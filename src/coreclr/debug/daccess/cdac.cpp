// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <stdio.h>
#include <minipal/utils.h>
#ifdef TARGET_UNIX
#include <pal.h>
#endif // TARGET_UNIX
#include <sospriv.h>
#include <sstring.h>
#include <clrhost.h>
#include "dbgutil.h"
#include "cdac_reader.h"
#include "cdac.h"

namespace {
static HRESULT
LoadCDACLibrary(HMODULE *phCDAC)
{
#ifndef TARGET_UNIX
    LPCWSTR pwzCDACName = MAKEDLLNAME_W(W("libcdacreader"));
#else
    LPCWSTR pwzCDACName = MAKEDLLNAME_W(W("cdacreader"));
#endif
    HRESULT hr = E_FAIL;

    // Load JIT from next to CoreCLR binary
    PathString CoreClrFolderHolder;
    if (GetClrModulePathName(CoreClrFolderHolder) && !CoreClrFolderHolder.IsEmpty())
    {
        SString::Iterator iter = CoreClrFolderHolder.End();
        BOOL findSep = CoreClrFolderHolder.FindBack(iter, DIRECTORY_SEPARATOR_CHAR_W);
        if (findSep)
        {
            SString sCDACName(pwzCDACName);
            CoreClrFolderHolder.Replace(iter + 1, CoreClrFolderHolder.End() - (iter + 1), sCDACName);

            *phCDAC = CLRLoadLibrary(CoreClrFolderHolder.GetUnicode());
            if (*phCDAC != NULL)
            {
                hr = S_OK;
            }
        }
   }
    return hr;
}

class CDACModuleLifetime {
public:
    static CDACModuleLifetime Create(HRESULT &result)
    {
        HMODULE hCDAC = nullptr;
        result = LoadCDACLibrary(&hCDAC);
        if (result != S_OK)
        {
            return CDACModuleLifetime();
        }
        return CDACModuleLifetime(hCDAC);
    }
    CDACModuleLifetime(const CDACModuleLifetime&) = delete;
    CDACModuleLifetime& operator=(const CDACModuleLifetime&) = delete;
    ~CDACModuleLifetime()
    {
        if (m_module)
        {
            FreeLibrary(m_module);
        }
    }
    CDACModuleLifetime(CDACModuleLifetime&& rhs) : m_module{rhs.m_module}
    {
        rhs.m_module = nullptr;
    }
    bool Valid() const
    {
	return m_module != nullptr;
    }
    FARPROC GetFn(const char *fn) const
    {
        return Valid() ? GetProcAddress(m_module, fn) : nullptr;
    }

    cdac_reader_result_t ReaderInit(cdac_reader_h *handle) const
    {
        if (!handle)
            return CDAC_READER_EFAIL;
        auto fn = GetFn("cdac_reader_init");
        if (!fn)
        {
            return CDAC_READER_EFAIL;
        }
        return reinterpret_cast<cdac_reader_result_t (*)(cdac_reader_h*)>(fn)(handle);
    }

private:
    CDACModuleLifetime() : m_module(nullptr) {}
    CDACModuleLifetime(HMODULE module) : m_module(module) {}
    HMODULE m_module;
};

} // anonymous namespace

class CDACImpl final {
public:
    explicit CDACImpl(CDACModuleLifetime&& module, cdac_reader_h handle, ICorDebugDataTarget* target)
        : m_module(std::move(module))
        , m_handle{handle}
        , m_target{target}
    {
        m_init = reinterpret_cast<decltype(&cdac_reader_init)>(m_module.GetFn("cdac_reader_init"));
        m_setReaderFunc = reinterpret_cast<decltype(&cdac_reader_set_reader_func)>(m_module.GetFn("cdac_reader_set_reader_func"));
        m_setStream = reinterpret_cast<decltype(&cdac_reader_set_stream)>(m_module.GetFn("cdac_reader_set_stream"));
        m_destroy = reinterpret_cast<decltype(&cdac_reader_destroy)>(m_module.GetFn("cdac_reader_destroy"));
        m_getBreakingChangeVersion = reinterpret_cast<decltype(&cdac_reader_get_breaking_change_version)>(m_module.GetFn("cdac_reader_get_breaking_change_version"));
    }

    CDACImpl(const CDACImpl&) = delete;
    CDACImpl& operator=(CDACImpl&) = delete;

    CDACImpl(CDACImpl&& other)
        : m_module(std::move(other.m_module))
        , m_handle{other.m_handle}
        , m_target{other.m_target}
        , m_init{other.m_init}
        , m_setReaderFunc{other.m_setReaderFunc}
        , m_setStream{other.m_setStream}
        , m_destroy{other.m_destroy}
        , m_getBreakingChangeVersion{other.m_getBreakingChangeVersion}
    {
    	other.m_handle = 0;
        other.m_target = nullptr;
    }
public:
    ~CDACImpl()
    {
        if (m_handle)
            m_destroy(m_handle);
    }

    cdac_reader_result_t Read(cdac_reader_foreignptr_t addr, uint32_t count, uint8_t *dest) const;
    cdac_reader_result_t SetReader() const;
    cdac_reader_result_t SetStream(TADDR data_stream) const;
    cdac_reader_result_t GetBreakingChangeVersion(int* version) const
    {
        if (m_getBreakingChangeVersion == nullptr)
            return CDAC_READER_EFAIL;

        return m_getBreakingChangeVersion(m_handle, version);
    }

private:
    CDACModuleLifetime m_module;
    cdac_reader_h m_handle;
    ICorDebugDataTarget* m_target;

private:
    decltype(&cdac_reader_init) m_init;
    decltype(&cdac_reader_set_reader_func) m_setReaderFunc;
    decltype(&cdac_reader_set_stream) m_setStream;
    decltype(&cdac_reader_destroy) m_destroy;
    decltype(&cdac_reader_get_breaking_change_version) m_getBreakingChangeVersion;

};

class FileHolder
{
public:
    explicit FileHolder(FILE* file) : m_file(file) {}
    ~FileHolder() { if (m_file) fclose(m_file); }
    FILE* Get() const { return m_file; }
private:
    FileHolder(const FileHolder& rhs) = delete;
    FileHolder& operator=(const FileHolder& rhs) = delete;
    FILE* m_file;
};

const CDAC* CDAC::CreateCDAC(TADDR data_stream, ICorDebugDataTarget* target)
{
#ifndef TARGET_UNIX
    FILE *f = fopen ("C:\\repos\\helloworld\\cdac.log", "a+"); // FIXME: remove this
    FileHolder fh {f};
#else
    FILE *f = stderr;
#endif
    // TODO: take the TADDR ClrDataAccess::m_globalBase and the TADDR of m_dacGlobals.g_data_streams_ptr
    fprintf (f, "initializing CDAC - alloc\n");
    HRESULT hr = S_OK;
    CDACModuleLifetime module {CDACModuleLifetime::Create(hr)};
    if (hr != S_OK)
    {
        fprintf(f, "initializing CDAC - failed 0x%08x\n", hr);
        return nullptr;
    }
    cdac_reader_result_t err = CDAC_READER_OK;
    cdac_reader_h handle = 0;

    fprintf (f, "initializing CDAC - call managed\n");
    if ((err = module.ReaderInit(&handle)) != CDAC_READER_OK)
    {
        fprintf(f, "cdac_reader_init failed 0x%08x\n", err);
        return nullptr;
    }
    fprintf(f, "initializing CDAC reader: GCHandle %p\n", (void*)handle);

    CDACImpl tmp{std::move(module), handle, target};
    CDACImpl *impl = new (nothrow) CDACImpl{std::move(tmp)};
    if (!impl)
        return nullptr;

    fprintf (f, "initializing CDAC - set reader\n");
    if ((err = impl->SetReader()) != CDAC_READER_OK)
    {
        fprintf (f, "cdac_reader_set_reader_func failed 0x%08x\n", err);
        delete impl;
        return nullptr;
    }

    fprintf (f, "initializing CDAC - set stream\n");
    if ((err = impl->SetStream(data_stream)) != CDAC_READER_OK)
    {
        fprintf (f, "cdac_reader_set_stream failed 0x%08x\n", err);
        delete impl;
        return nullptr;
    }

    fprintf (f, "initializing CDAC - done\n");
    CDAC *cdac = new (nothrow) CDAC(impl);
    if (!cdac)
    {
        delete impl;
        return nullptr;
    }

    impl = nullptr;

    fprintf (f, "initializing CDAC - done\n");
    return cdac;
}

CDAC::CDAC(CDACImpl *impl) : m_impl(impl)
{
}

CDAC::~CDAC()
{
    delete m_impl;
}

namespace
{
    extern "C" {
	cdac_reader_result_t ReaderCB (cdac_reader_foreignptr_t addr, uint32_t count, void* user_data, uint8_t *dest)
	{
	    const CDACImpl* cdac = reinterpret_cast<const CDACImpl*>(user_data);
	    return cdac->Read (addr, count, dest);
	}
    }
}

cdac_reader_result_t CDACImpl::SetReader () const
{
    if (m_setReaderFunc == nullptr)
        return CDAC_READER_EFAIL;

    return m_setReaderFunc(m_handle, &ReaderCB, reinterpret_cast<void*>(const_cast<CDACImpl*>(this)));
}

cdac_reader_result_t CDACImpl::SetStream(TADDR data_stream) const
{
    if (m_setStream == nullptr)
        return CDAC_READER_EFAIL;

    return m_setStream(m_handle, data_stream);
}

cdac_reader_result_t CDACImpl::Read(cdac_reader_foreignptr_t addr, uint32_t count, uint8_t *dest) const
{
    // FIXME: comment in dbgutil.cpp says ReadFromDataTarget throws
    fprintf(stderr, "reading %d bytes from %p\n", (int)count, (void*)addr);
    HRESULT hr = ReadFromDataTarget(m_target, static_cast<ULONG64>(addr), static_cast<BYTE*>(dest), static_cast<ULONG32>(count));
    if (FAILED(hr))
        return CDAC_READER_EFAIL;

    return CDAC_READER_OK;
}

HRESULT STDMETHODCALLTYPE CDAC::GetBreakingChangeVersion(int* pVersion) const
{
    auto result = m_impl->GetBreakingChangeVersion(pVersion);
    if (result != CDAC_READER_OK)
        return E_FAIL;

    return S_OK;
}
