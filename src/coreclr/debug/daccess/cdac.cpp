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

class CDACModuleHolder {
public:
    static CDACModuleHolder Create(HRESULT &result)
    {
        HMODULE hCDAC = nullptr;
        result = LoadCDACLibrary(&hCDAC);
        if (result != S_OK)
        {
            return CDACModuleHolder();
        }
        return CDACModuleHolder(hCDAC);
    }
    CDACModuleHolder(const CDACModuleHolder&) = delete;
    CDACModuleHolder& operator=(const CDACModuleHolder&) = delete;
    ~CDACModuleHolder()
    {
        if (m_module)
        {
            FreeLibrary(m_module);
        }
    }
    CDACModuleHolder(CDACModuleHolder&& rhs) : m_module(rhs.m_module)
    {
        rhs.m_module = nullptr;
    }
    void *GetFn(const char *fn) const
    {
        return m_module ? GetProcAddress(m_module, fn) : nullptr;
    }
private:
    CDACModuleHolder() : m_module(nullptr) {}
    CDACModuleHolder(HMODULE module) : m_module(module) {}
    HMODULE m_module;
};

}

class CDACImpl final {
public:
    explicit CDACImpl(CDACModuleHolder&& holder) : m_holder(std::move(holder)) {}
public:
    ~CDACImpl() {}
    static cdac_reader_result_t ReaderCB (cdac_reader_foreignptr_t addr, uint32_t count, void* user_data, uint8_t *dest);
    cdac_reader_result_t ReaderInit(cdac_reader_h *handle) const
    {
        auto fn = m_holder.GetFn("cdac_reader_init");
        if (!fn)
        {
            return CDAC_READER_EFAIL;
        }
        return reinterpret_cast<cdac_reader_result_t (*)(cdac_reader_h*)>(fn)(handle);
    }
private:
    CDACModuleHolder m_holder;
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

const CDAC* CDAC::CreateCDAC()
{
    FILE *f = fopen ("E:\\cdac.log", "a+");
    FileHolder fh {f};
    // TODO: take the TADDR ClrDataAccess::m_globalBase and the TADDR of m_dacGlobals.g_data_streams_ptr
    fprintf (f, "initializing CDAC - alloc\n");
    HRESULT hr = S_OK;
    CDACModuleHolder holder {CDACModuleHolder::Create(hr)};
    if (hr != S_OK)
    {
        fprintf(f, "initializing CDAC - failed 0x%08x\n", hr);
        return nullptr;
    }
    CDACImpl *impl = new (nothrow) CDACImpl(std::move(holder));
    if (!impl)
    {
        return nullptr;
    }
    CDAC *cdac = new (nothrow) CDAC(impl);
    if (!cdac)
    {
        delete impl;
        return nullptr;
    }
    impl = nullptr;
    fprintf (f, "initializing CDAC - call managed\n");
    cdac_reader_result_t result = 0;
    if ((result = cdac->m_impl->ReaderInit (&cdac->m_handle)) < 0)
    {
        return nullptr;
    }
    fprintf(f, "initializing CDAC: result %d GCHandle %p\n", result, (void*)cdac->m_handle);
#if 0
    if (cdac_reader_init (&cdac->m_handle) < 0)
    {
        return nullptr;
    }
    fprintf (f, "initializing CDAC - set reader\n");
    if (cdac_reader_set_reader_func (cdac->m_handle, &CDACImpl::ReaderCB, cdac) < 0)
    {
        return nullptr;
    }
#endif
    fprintf (f, "initializing CDAC - done\n");
    return cdac;
}

CDAC::CDAC(CDACImpl *impl) : m_handle(0), m_impl(impl)
{
}

CDAC::~CDAC()
{
# if 0
    if (m_handle)
        cdac_reader_destroy (m_handle);
#endif
    delete m_impl;
}

cdac_reader_result_t CDACImpl::ReaderCB (cdac_reader_foreignptr_t addr, uint32_t count, void* user_data, uint8_t *dest)
{
    const CDAC* cdac = reinterpret_cast<const CDAC*>(user_data);
    return cdac->Read (addr, count, dest);
}

cdac_reader_result_t CDAC::Read(cdac_reader_foreignptr_t addr, uint32_t count, uint8_t *dest) const
{
    return CDAC_READER_EFAIL; // TODO: implement me
}

HRESULT STDMETHODCALLTYPE CDAC::GetBreakingChangeVersion(int* pVersion) const
{
    // cdac_reader_QueryInterface_ISOSDacInterface9()->get_breaking_change_version(handle)
    *pVersion = SOS_BREAKING_CHANGE_VERSION;
    return S_OK;
}
