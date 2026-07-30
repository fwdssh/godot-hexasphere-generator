#ifndef ANKERL_STL_H
#define ANKERL_STL_H

#include <array>            // for array
#include <cstdint>          // for uint64_t, uint32_t, std::uint8_t, UINT64_C
#include <cstring>          // for size_t, memcpy, memset
#include <functional>       // for equal_to, hash
#include <initializer_list> // for initializer_list
#include <iterator>         // for pair, distance
#include <limits>           // for numeric_limits
#include <memory>           // for allocator, allocator_traits, shared_ptr
#include <optional>         // for optional
#include <stdexcept>        // for out_of_range
#include <string>           // for basic_string
#include <string_view>      // for basic_string_view, hash
#include <tuple>            // for forward_as_tuple
#include <type_traits>      // for enable_if_t, declval, conditional_t, ena...
#include <utility>          // for forward, exchange, pair, as_const, piece...
#include <vector>           // for vector

// <memory_resource> includes <mutex>, which fails to compile if
// targeting GCC >= 13 with the (rewritten) win32 thread model, and
// targeting Windows earlier than Vista (0x600).  GCC predefines
// _REENTRANT when using the 'posix' model, and doesn't when using the
// 'win32' model.
#if defined __MINGW64__ && defined __GNUC__ && __GNUC__ >= 13 && !defined _REENTRANT
// _WIN32_WINNT is guaranteed to be defined here because of the
// <cstdint> inclusion above.
#    ifndef _WIN32_WINNT
#        error "_WIN32_WINNT not defined"
#    endif
#    if _WIN32_WINNT < 0x600
#        define ANKERL_MEMORY_RESOURCE_IS_BAD() 1
#    endif
#endif
#ifndef ANKERL_MEMORY_RESOURCE_IS_BAD
#    define ANKERL_MEMORY_RESOURCE_IS_BAD() 0
#endif

#if defined(__has_include) && !defined(ANKERL_UNORDERED_DENSE_DISABLE_PMR)
#    if __has_include(<memory_resource>) && !ANKERL_MEMORY_RESOURCE_IS_BAD()
#        define ANKERL_UNORDERED_DENSE_PMR std::pmr
#        include <memory_resource>
#    elif __has_include(<experimental/memory_resource>)
#        define ANKERL_UNORDERED_DENSE_PMR std::experimental::pmr
#        include <experimental/memory_resource>
#    endif
#endif

#if defined(_MSC_VER) && defined(_M_X64)
#    include <intrin.h>
#    if !defined(_M_ARM64EC)
#        pragma intrinsic(_umul128)
#    endif
#endif

#endif
