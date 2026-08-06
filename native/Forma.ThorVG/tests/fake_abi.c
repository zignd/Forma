#include <stdint.h>

#ifndef FORMA_THORVG_FAKE_ABI
#define FORMA_THORVG_FAKE_ABI 1
#endif

uint32_t forma_thorvg_abi_version(void)
{
    return FORMA_THORVG_FAKE_ABI;
}