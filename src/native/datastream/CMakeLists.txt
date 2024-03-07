set(CMAKE_INCLUDE_CURRENT_DIR ON)

set(DATASTREAM_SOURCES
    data_stream.c
    data_stream.h
)

add_library(datastream OBJECT ${DATASTREAM_SOURCES})

install(FILES $<TARGET_OBJECTS:datastream> DESTINATION datastream COMPONENT cdac RENAME datastream.obj)