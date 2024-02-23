
add_library(cdac_contract INTERFACE
  cdac/cdac/ds_types.h
)

target_include_directories(cdac_contract INTERFACE ${CMAKE_CURRENT_SOURCE_DIR}/cdac)

