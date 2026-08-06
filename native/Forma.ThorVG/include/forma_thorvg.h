#ifndef FORMA_THORVG_H
#define FORMA_THORVG_H

#include <stddef.h>
#include <stdint.h>

#if defined(_WIN32)
#define FORMA_THORVG_EXPORT __declspec(dllexport)
#else
#define FORMA_THORVG_EXPORT __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

#define FORMA_THORVG_ABI_VERSION 1u

typedef struct FormaThorvgDocument FormaThorvgDocument;

typedef enum FormaThorvgResult {
    FORMA_THORVG_SUCCESS = 0,
    FORMA_THORVG_INVALID_ARGUMENT = 1,
    FORMA_THORVG_OUT_OF_MEMORY = 2,
    FORMA_THORVG_PARSE_FAILED = 3,
    FORMA_THORVG_RENDER_FAILED = 4,
    FORMA_THORVG_ENGINE_FAILED = 5
} FormaThorvgResult;

FORMA_THORVG_EXPORT uint32_t forma_thorvg_abi_version(void);
FORMA_THORVG_EXPORT const char* forma_thorvg_version(void);
FORMA_THORVG_EXPORT size_t forma_thorvg_last_error(char* output, size_t output_size);
FORMA_THORVG_EXPORT FormaThorvgResult forma_thorvg_initialize(void);
FORMA_THORVG_EXPORT FormaThorvgResult forma_thorvg_terminate(void);
FORMA_THORVG_EXPORT FormaThorvgResult forma_thorvg_document_create(
    const uint8_t* svg,
    size_t svg_size,
    FormaThorvgDocument** document);
FORMA_THORVG_EXPORT void forma_thorvg_document_destroy(FormaThorvgDocument* document);
FORMA_THORVG_EXPORT FormaThorvgResult forma_thorvg_document_size(
    const FormaThorvgDocument* document,
    float* width,
    float* height);
FORMA_THORVG_EXPORT FormaThorvgResult forma_thorvg_document_rasterize(
    const FormaThorvgDocument* document,
    uint32_t width,
    uint32_t height,
    uint8_t* rgba,
    size_t rgba_size);

#ifdef __cplusplus
}
#endif

#endif