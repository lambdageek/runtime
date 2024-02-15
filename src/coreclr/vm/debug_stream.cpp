// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "debug_stream.h"
#include <minipal/utils.h>
#include <stdio.h>

#ifdef TARGET_UNIX
#include <pal.h>
#endif // TARGET_UNIX

namespace
{
    data_stream_context_t g_data_streams;
}

bool debug_stream::init()
{
    size_t sizes[] = { 4096, 8192, 2048 };
    if (!dnds_init(&g_data_streams, ARRAY_SIZE(sizes), sizes))
        return false;

#ifdef DEBUG
    printf("DS: %u %p\n", GetCurrentProcessId(), &g_data_streams);
#endif // DEBUG

    return true;
}
