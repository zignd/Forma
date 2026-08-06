#include "forma_thorvg.h"

#include <cmath>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <vector>

namespace {

bool near(uint8_t actual, uint8_t expected)
{
    return std::abs(static_cast<int>(actual) - static_cast<int>(expected)) <= 1;
}

int fail(const char* message)
{
    std::fprintf(stderr, "ThorVG smoke failed: %s\n", message);
    return 1;
}

}

int main()
{
    constexpr char svg[] =
        "<svg xmlns='http://www.w3.org/2000/svg' width='2' height='2' viewBox='0 0 2 2'>"
        "<rect width='2' height='2' fill='#ff0000' fill-opacity='0.5'/>"
        "</svg>";

    if (forma_thorvg_abi_version() != FORMA_THORVG_ABI_VERSION) return fail("ABI version mismatch");
    if (forma_thorvg_initialize() != FORMA_THORVG_SUCCESS) return fail("engine initialization");

    FormaThorvgDocument* invalid_document = nullptr;
    if (forma_thorvg_document_create(nullptr, 0, &invalid_document) != FORMA_THORVG_INVALID_ARGUMENT)
        return fail("null source rejection");
    char error[16] = {};
    if (forma_thorvg_last_error(error, sizeof(error)) <= sizeof(error) || error[sizeof(error) - 1] != '\0')
        return fail("bounded error truncation");

    constexpr char truncated[] = "<svg";
    if (forma_thorvg_document_create(
            reinterpret_cast<const uint8_t*>(truncated),
            std::strlen(truncated),
            &invalid_document) != FORMA_THORVG_PARSE_FAILED)
        return fail("truncated SVG rejection");

    FormaThorvgDocument* document = nullptr;
    if (forma_thorvg_document_create(
            reinterpret_cast<const uint8_t*>(svg),
            std::strlen(svg),
            &document) != FORMA_THORVG_SUCCESS) {
        return fail("in-memory SVG parse");
    }

    float width = 0;
    float height = 0;
    if (forma_thorvg_document_size(document, &width, &height) != FORMA_THORVG_SUCCESS ||
        width != 2 || height != 2) {
        return fail("intrinsic dimensions");
    }

    uint8_t pixels[16] = {};
    if (forma_thorvg_document_rasterize(document, 2, 2, pixels, sizeof(pixels) - 1) != FORMA_THORVG_INVALID_ARGUMENT)
        return fail("undersized output rejection");
    if (forma_thorvg_document_rasterize(document, UINT32_MAX, UINT32_MAX, pixels, sizeof(pixels)) != FORMA_THORVG_INVALID_ARGUMENT)
        return fail("overflowing dimensions rejection");
    if (forma_thorvg_document_rasterize(document, 2, 2, pixels, sizeof(pixels)) != FORMA_THORVG_SUCCESS) {
        return fail("rasterization");
    }

    for (size_t offset = 0; offset < sizeof(pixels); offset += 4) {
        if (!near(pixels[offset], 128) || pixels[offset + 1] != 0 ||
            pixels[offset + 2] != 0 || !near(pixels[offset + 3], 128)) {
            std::fprintf(
                stderr,
                "ThorVG smoke failed: expected premultiplied RGBA 128,0,0,128; got %u,%u,%u,%u\n",
                pixels[offset],
                pixels[offset + 1],
                pixels[offset + 2],
                pixels[offset + 3]);
            return 1;
        }
    }

    forma_thorvg_document_destroy(document);
    forma_thorvg_document_destroy(nullptr);
    forma_thorvg_document_destroy(nullptr);

    for (int iteration = 0; iteration < 1000; ++iteration) {
        FormaThorvgDocument* repeated = nullptr;
        if (forma_thorvg_document_create(
                reinterpret_cast<const uint8_t*>(svg),
                std::strlen(svg),
                &repeated) != FORMA_THORVG_SUCCESS)
            return fail("repeated parse");
        if (forma_thorvg_document_rasterize(repeated, 2, 2, pixels, sizeof(pixels)) != FORMA_THORVG_SUCCESS)
            return fail("repeated raster");
        forma_thorvg_document_destroy(repeated);
    }

    const auto* svg_bytes = reinterpret_cast<const uint8_t*>(svg);
    const size_t svg_size = std::strlen(svg);
    for (size_t prefix_size = 1; prefix_size < svg_size; ++prefix_size) {
        FormaThorvgDocument* prefix_document = nullptr;
        const auto result = forma_thorvg_document_create(svg_bytes, prefix_size, &prefix_document);
        if (result == FORMA_THORVG_SUCCESS) {
            forma_thorvg_document_destroy(prefix_document);
        } else if (prefix_document != nullptr) {
            return fail("rejected prefix returned a document");
        }
    }

    uint32_t random_state = 0x464f524d;
    for (int iteration = 0; iteration < 1000; ++iteration) {
        std::vector<uint8_t> mutated(svg_bytes, svg_bytes + svg_size);
        random_state = random_state * 1664525u + 1013904223u;
        const size_t position = random_state % mutated.size();
        mutated[position] ^= static_cast<uint8_t>((random_state >> 24) | 1u);
        FormaThorvgDocument* mutated_document = nullptr;
        const auto result = forma_thorvg_document_create(mutated.data(), mutated.size(), &mutated_document);
        if (result == FORMA_THORVG_SUCCESS) {
            forma_thorvg_document_destroy(mutated_document);
        } else if (mutated_document != nullptr) {
            return fail("rejected mutation returned a document");
        }
    }

    constexpr const char* raster_seeds[] = {
        "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><path d='M0 0h8v8H0z' fill='#f00'/></svg>",
        "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><circle cx='4' cy='4' r='3' fill='#0f0'/></svg>",
        "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><rect x='1' y='1' width='6' height='6' transform='rotate(15 4 4)' fill='#00f'/></svg>",
        "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><defs><linearGradient id='g'><stop stop-color='#fff'/><stop offset='1' stop-color='#000'/></linearGradient></defs><rect width='8' height='8' fill='url(#g)'/></svg>",
        "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><path d='M1 7V1H7' fill='none' stroke='#fff' stroke-width='1' stroke-dasharray='2 1'/></svg>",
    };
    uint8_t seed_pixels[8 * 8 * 4] = {};
    for (const auto* raster_seed : raster_seeds) {
        FormaThorvgDocument* seed_document = nullptr;
        if (forma_thorvg_document_create(
                reinterpret_cast<const uint8_t*>(raster_seed),
                std::strlen(raster_seed),
                &seed_document) != FORMA_THORVG_SUCCESS)
            return fail("bounded raster seed parse");
        if (forma_thorvg_document_rasterize(seed_document, 8, 8, seed_pixels, sizeof(seed_pixels)) != FORMA_THORVG_SUCCESS)
            return fail("bounded raster seed output");
        forma_thorvg_document_destroy(seed_document);
    }

    if (forma_thorvg_terminate() != FORMA_THORVG_SUCCESS) return fail("engine termination");

    std::printf(
        "ThorVG %s ABI %u rendered in-memory premultiplied RGBA8 successfully.\n",
        forma_thorvg_version(),
        forma_thorvg_abi_version());
    return 0;
}