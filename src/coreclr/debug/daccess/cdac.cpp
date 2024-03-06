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

    void ReaderDestroy(cdac_reader_h handle) const
    {
	if (handle) {
	    auto fn = GetFn("cdac_reader_destroy");
	    if (fn) {
		reinterpret_cast<void(*)(cdac_reader_h)>(fn)(handle);
	    }
	}
    }
private:
    CDACModuleLifetime() : m_module(nullptr) {}
    CDACModuleLifetime(HMODULE module) : m_module(module) {}
    HMODULE m_module;
};

} // anonymous namespace

class CDACImpl final {
public:
    explicit CDACImpl(CDACModuleLifetime&& module, cdac_reader_h handle) :
	m_module(std::move(module)), m_handle(handle) {}
    CDACImpl(const CDACImpl&) = delete;
    CDACImpl& operator=(CDACImpl&) = delete;
    CDACImpl(CDACImpl&& other) : m_module(std::move(other.m_module)), m_handle{other.m_handle}
    {
	other.m_handle = 0;
    }
public:
    ~CDACImpl() {
	if (m_handle)
	{
	    m_module.ReaderDestroy(m_handle);
	}
    }
    cdac_reader_result_t Read(cdac_reader_foreignptr_t addr, uint32_t count, uint8_t *dest) const;

    cdac_reader_result_t SetReader() const;
    cdac_reader_result_t SetStream(TADDR data_stream) const;
private:
    CDACModuleLifetime m_module;
    cdac_reader_h m_handle;
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

const CDAC* CDAC::CreateCDAC(TADDR data_stream)
{
#ifndef TARGET_UNIX
    FILE *f = fopen ("E:\\cdac.log", "a+"); // FIXME: remove this
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
    if ((err = module.ReaderInit(&handle)) != CDAC_READER_OK) {
	fprintf(f, "cdac_reader_init failed 0x%08x\n", err);
	return nullptr;
    }
    fprintf(f, "initializing CDAC reader: GCHandle %p\n", (void*)handle);
    
    CDACImpl tmp{std::move(module), handle};
    CDACImpl *impl = new (nothrow) CDACImpl{std::move(tmp)};
    if (!impl)
    {
        return nullptr;
    }
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
    auto fn = m_module.GetFn("cdac_reader_set_reader_func");
    if (!fn)
    {
	return CDAC_READER_EFAIL;
    }
    return reinterpret_cast<cdac_reader_result_t(*)(cdac_reader_h, cdac_reader_func_t, void*)>(fn)(m_handle, &ReaderCB, reinterpret_cast<void*>(const_cast<CDACImpl*>(this)));
}

cdac_reader_result_t CDACImpl::SetStream(TADDR data_stream) const
{
    auto fn = m_module.GetFn("cdac_reader_set_stream");
    if (!fn)
    {
	return CDAC_READER_EFAIL;
    }
    return reinterpret_cast<cdac_reader_result_t(*)(cdac_reader_h, cdac_reader_foreignptr_t)>(fn)(m_handle, static_cast<cdac_reader_foreignptr_t>(data_stream));
}

cdac_reader_result_t CDACImpl::Read(cdac_reader_foreignptr_t addr, uint32_t count, uint8_t *dest) const
{
    return CDAC_READER_EFAIL; // TODO: implement me
}

HRESULT STDMETHODCALLTYPE CDAC::GetBreakingChangeVersion(int* pVersion) const
{
    // cdac_reader_QueryInterface_ISOSDacInterface9()->get_breaking_change_version(handle)
    *pVersion = SOS_BREAKING_CHANGE_VERSION;
    return S_OK;
}
