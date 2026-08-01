cmake_minimum_required(VERSION 3.24)

project(
  ifm_databento_feed_native
  VERSION 0.1.0
  DESCRIPTION "IFM native Databento market-data bridge"
  LANGUAGES CXX
)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

option(IFM_DATABENTO_ENABLE_LIVE "Build the licensed Databento live adapter" OFF)
option(IFM_DATABENTO_BUILD_TESTS "Build native synthetic tests" ON)

include(cmake/DatabentoVersion.cmake)

if(IFM_DATABENTO_ENABLE_LIVE)
  include(cmake/DatabentoDependency.cmake)
endif()

configure_file(
  include/databento_dependency_metadata.h.in
  generated/databento_dependency_metadata.h
  @ONLY
)

add_library(ifm_databento_sdk INTERFACE)
add_library(IFM::DatabentoSdk ALIAS ifm_databento_sdk)

if(IFM_DATABENTO_ENABLE_LIVE)
  target_link_libraries(ifm_databento_sdk INTERFACE databento::databento)
endif()

target_include_directories(
  ifm_databento_sdk
  INTERFACE
    $<BUILD_INTERFACE:${CMAKE_CURRENT_BINARY_DIR}/generated>
)

add_library(databento_feed_native SHARED
  src/databento_feed_native.cpp
)

target_include_directories(databento_feed_native
  PUBLIC
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
  PRIVATE
    ${CMAKE_CURRENT_BINARY_DIR}/generated
)

target_compile_definitions(databento_feed_native
  PRIVATE
    DBF_NATIVE_BUILD
    $<$<BOOL:${IFM_DATABENTO_ENABLE_LIVE}>:DBF_ENABLE_LIVE>
)

target_link_libraries(databento_feed_native PRIVATE IFM::DatabentoSdk)

if(IFM_DATABENTO_ENABLE_LIVE AND WIN32)
  add_custom_command(
    TARGET databento_feed_native
    POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
            $<TARGET_RUNTIME_DLLS:databento_feed_native>
            $<TARGET_FILE_DIR:databento_feed_native>
    COMMAND_EXPAND_LISTS
  )
endif()

if(MSVC)
  target_compile_options(databento_feed_native PRIVATE /W4 /WX /permissive- /wd4324)
else()
  target_compile_options(databento_feed_native PRIVATE -Wall -Wextra -Wpedantic -Werror)
endif()

set_target_properties(databento_feed_native PROPERTIES
  OUTPUT_NAME databento_feed_native
  CXX_VISIBILITY_PRESET hidden
  VISIBILITY_INLINES_HIDDEN YES
)

if(IFM_DATABENTO_BUILD_TESTS)
  enable_testing()
  add_executable(databento_feed_native_tests
    tests/databento_feed_native_tests.cpp
  )
  target_link_libraries(databento_feed_native_tests PRIVATE databento_feed_native)
  target_include_directories(databento_feed_native_tests PRIVATE src)
  if(IFM_DATABENTO_ENABLE_LIVE)
    target_compile_definitions(databento_feed_native_tests PRIVATE DBF_ENABLE_LIVE)
    target_link_libraries(databento_feed_native_tests PRIVATE IFM::DatabentoSdk)
  endif()
  target_compile_features(databento_feed_native_tests PRIVATE cxx_std_20)
  if(MSVC)
    target_compile_options(databento_feed_native_tests PRIVATE /W4 /WX /permissive-)
  else()
    target_compile_options(databento_feed_native_tests PRIVATE -Wall -Wextra -Wpedantic -Werror)
  endif()
  add_test(NAME databento_feed_native_tests COMMAND databento_feed_native_tests)
endif()
