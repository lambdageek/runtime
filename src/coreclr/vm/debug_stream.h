// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef DEBUG_STREAM_H
#define DEBUG_STREAM_H

#include <datastream/data_stream.h>

namespace dk
{
#define MAKE_TYPE_ID(type) dk_ ## type
#define BEGIN_TYPE_IDS() typedef enum dk_type__ {
#define END_TYPE_IDS() } dk_type_t;
#define DEFINE_TYPE_ID(id, v) MAKE_TYPE_ID(id),
#define DEFINE_TYPE_ID_LAST() DEFINE_TYPE_ID(Count, 0)
#include <cdac/ds_types.h>
static_assert(dk_Count <= DNDS_MAX_TYPE_SIZE, "Type count is limited to max value for type");
}

namespace debug_stream
{
    bool init();

    void define_type(dk::dk_type_t type, size_t total_size, size_t offsets_length = 0, field_offset_t const* offsets = nullptr);

    template<size_t C>
    void define_type(dk::dk_type_t type, size_t total_size, field_offset_t const (&offsets)[C])
    {
        define_type(type, total_size, C, offsets);
    }

    void register_basic_types();

    // void record_blob(dk_type_t type, uint16_t size, void* addr);

    //void record_instance(dk_type_t type, void* addr);
}

#endif // DEBUG_STREAM_H
