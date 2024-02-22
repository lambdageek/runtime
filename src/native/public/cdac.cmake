
add_library(cdac_contract INTERFACE)
target_sources(cdac_contract
  cdac/cdac/ds_types.h
)

target_include_directories(cdac_contract INTERFACE cdac)

