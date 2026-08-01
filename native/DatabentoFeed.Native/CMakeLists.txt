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

include(cmake/DatabentoDependency.cmake)

configure_file(
  include/databento_dependency_metadata.h.in
  generated/databento_dependency_metadata.h
  @ONLY
)

# The native feed target will link to this stable IFM-owned target when the
# feed implementation begins. Keeping the vendor target behind this boundary
# prevents Databento's CMake target name from leaking through the project.
add_library(ifm_databento_sdk INTERFACE)
add_library(IFM::DatabentoSdk ALIAS ifm_databento_sdk)

target_link_libraries(
  ifm_databento_sdk
  INTERFACE
    databento::databento
)

target_include_directories(
  ifm_databento_sdk
  INTERFACE
    $<BUILD_INTERFACE:${CMAKE_CURRENT_BINARY_DIR}/generated>
)
