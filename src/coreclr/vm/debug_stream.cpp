// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <stdio.h>
#include <minipal/utils.h>
#include <daccess.h>
#include <sospriv.h>

#include "debug_stream.h"

namespace
{
    data_stream_context_t g_data_streams;

#define BEGIN_TYPE_IDS() type_details_t const g_type_types[] = {
#define END_TYPE_IDS() };
#define DEFINE_TYPE_ID(id, v) { dk::MAKE_TYPE_ID(id), v, 0, sizeof(#id), #id },
#include <cdac/ds_types.h>
    static_assert(dk::dk_Count == ARRAY_SIZE(g_type_types), "All types.h uses should be the same size");
}

// see daccess.h
// HACK: use the existing non-portable DAC mechanisms to find the cDAC streams
namespace debug_stream
{
    namespace priv {
	GPTR_IMPL_INIT(BYTE, g_data_streams_ptr, reinterpret_cast<BYTE*>(&g_data_streams));
    }
}

#ifndef DACCESS_COMPILE
bool debug_stream::init()
{
    size_t sizes[] = { 4096, 8192, 2048 };
    if (!dnds_init(&g_data_streams, ARRAY_SIZE(sizes), sizes))
        return false;

#ifdef DEBUG
    printf("DS: %p\n", &g_data_streams);
#endif // DEBUG

    return true;
}

void debug_stream::define_type(dk::dk_type_t type, size_t total_size, size_t offsets_length, field_offset_t const* offsets)
{
    _ASSERTE_ALL_BUILDS(dnds_define_type(&g_data_streams, &g_type_types[type],  total_size, offsets_length, offsets));
}

void debug_stream::record_blob(dk::dk_type_t type, uint16_t size, void* addr)
{
    dnds_record_blob(dnds_get_stream(&g_data_streams, 1), (uint16_t)type, size, addr);
}

void debug_stream::register_basic_types()
{
    // Types
    debug_stream::define_type(dk::MAKE_TYPE_ID(Ptr), sizeof(void*));
    debug_stream::define_type(dk::MAKE_TYPE_ID(ThreadStore), sizeof(ThreadStore));
    // Data
    intptr_t version_data[]
    {
        SOS_BREAKING_CHANGE_VERSION
    };
    debug_stream::record_blob(dk::MAKE_TYPE_ID(SOSBreakingChangeVersion), sizeof(uint32_t), version_data);
}
#endif /*DACCESS_COMPILE*/
