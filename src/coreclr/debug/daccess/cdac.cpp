// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <stdio.h>
#include <minipal/utils.h>
#ifdef TARGET_UNIX
#include <pal.h>
#endif // TARGET_UNIX
#include <sospriv.h>
#include "cdac_reader.h"
#include "cdac.h"

class CDACImpl final {
private:
    explicit CDACImpl() {}
    ~CDACImpl() {}
public:
    static cdac_reader_result_t ReaderCB (cdac_reader_foreignptr_t addr, uint32_t count, void* user_data, uint8_t *dest);
};

const CDAC* CDAC::CreateCDAC()
{
    // TODO: take the TADDR ClrDataAccess::m_globalBase and the TADDR of m_dacGlobals.g_data_streams_ptr
    printf ("initializing CDAC - alloc\n");
    CDAC *cdac = new (nothrow) CDAC();
    if (!cdac)
    {
	return nullptr;
    }
    printf ("initializing CDAC - call managed\n");
    if (cdac_reader_init (&cdac->m_handle) < 0)
    {
	return nullptr;
    }
    printf ("initializing CDAC - set reader\n");
    if (cdac_reader_set_reader_func (cdac->m_handle, &CDACImpl::ReaderCB, cdac) < 0)
    {
	return nullptr;
    }
    printf ("initializing CDAC - done\n");
    return cdac;
}

CDAC::CDAC() : m_handle(0)
{
}

CDAC::~CDAC()
{
    if (m_handle)
	cdac_reader_destroy (m_handle);
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
