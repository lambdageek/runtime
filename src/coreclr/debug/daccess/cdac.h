// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef __CDAC_H__
#define __CDAC_H__

#include "cdac_reader.h"


class CDACImpl;

class CDAC final /*: public ISOSDacInterface9*/
{
public:
    static const CDAC* CreateCDAC(TADDR data_stream);
    virtual ~CDAC();
    CDAC(const CDAC&) = delete;
    CDAC& operator=(const CDAC&) = delete;
private:
    explicit CDAC(CDACImpl *impl);

public:
    /*virtual HRESULT STDMETHODCALLTYPE GetBreakingChangeVersion(int* pVersion);*/ // const correctness
    HRESULT STDMETHODCALLTYPE GetBreakingChangeVersion(int* pVersion) const;

private:
    CDACImpl *m_impl;

    friend class CDACImpl;
};

#endif /* __CDAC_H__ */
