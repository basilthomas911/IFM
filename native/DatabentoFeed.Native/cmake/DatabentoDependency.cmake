include_guard(GLOBAL)

include(FetchContent)
include(${CMAKE_CURRENT_LIST_DIR}/DatabentoVersion.cmake)

# Databento's own unit tests and examples are not part of the IFM build. IFM
# tests exercise the pinned SDK through the native bridge instead.
set(DATABENTO_ENABLE_UNIT_TESTING OFF CACHE BOOL "" FORCE)
set(DATABENTO_ENABLE_EXAMPLES OFF CACHE BOOL "" FORCE)

message(
  STATUS
  "Configuring Databento ${IFM_DATABENTO_VERSION} at ${IFM_DATABENTO_COMMIT}"
)

FetchContent_Declare(
  databento
  GIT_REPOSITORY https://github.com/databento/databento-cpp.git
  GIT_TAG ${IFM_DATABENTO_COMMIT}
  GIT_PROGRESS TRUE
  UPDATE_DISCONNECTED TRUE
)

FetchContent_MakeAvailable(databento)

if(NOT TARGET databento::databento)
  message(FATAL_ERROR "The pinned Databento source did not define databento::databento")
endif()
