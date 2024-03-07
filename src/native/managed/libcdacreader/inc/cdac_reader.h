#ifndef _CDAC_READER_H
#define _CDAC_READER_H

#ifdef __cplusplus
extern "C"
{
#endif

enum cdac_reader_result_enum {
    CDAC_READER_OK = 0,
    CDAC_READER_EFAIL = -1,
};

typedef int32_t cdac_reader_result_t;

typedef intptr_t cdac_reader_h;
typedef uint64_t cdac_reader_foreignptr_t;

cdac_reader_result_t cdac_reader_init(const char *ownBaseDir, cdac_reader_h *handleOut);

typedef cdac_reader_result_t (*cdac_reader_func_t) (cdac_reader_foreignptr_t addr, uint32_t count, void* user_data, uint8_t *dest);

cdac_reader_result_t cdac_reader_set_reader_func(cdac_reader_h handle, cdac_reader_func_t reader, void* user_data);

cdac_reader_result_t cdac_reader_set_stream(cdac_reader_h handle, cdac_reader_foreignptr_t stream_start);

void cdac_reader_destroy(cdac_reader_h handle);

// [TODO: cDAC] This is currently just for initial testing getting data from the stream
cdac_reader_result_t cdac_reader_get_breaking_change_version(cdac_reader_h handle, int* version);

#ifdef __cplusplus
}
#endif

#endif/*_CDAC_READER_H*/
