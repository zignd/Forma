#include "forma_thorvg.h"

#include <cstdlib>
#include <cstdio>
#include <cstring>
#include <limits>

#include <thorvg.h>

struct FormaThorvgDocument {
    tvg::Picture* picture;
};

namespace {

thread_local char last_error[256] = {};

bool succeeded(tvg::Result result)
{
    return result == tvg::Result::Success;
}

FormaThorvgResult fail(FormaThorvgResult result, const char* message)
{
    std::snprintf(last_error, sizeof(last_error), "%s", message);
    return result;
}

void clear_error()
{
    last_error[0] = '\0';
}

}

uint32_t forma_thorvg_abi_version(void)
{
    return FORMA_THORVG_ABI_VERSION;
}

const char* forma_thorvg_version(void)
{
    return tvg::Initializer::version(nullptr, nullptr, nullptr);
}

size_t forma_thorvg_last_error(char* output, size_t output_size)
{
    const size_t required = std::strlen(last_error) + 1;
    if (!output || output_size == 0) return required;
    std::snprintf(output, output_size, "%s", last_error);
    return required;
}

FormaThorvgResult forma_thorvg_initialize(void)
{
    if (!succeeded(tvg::Initializer::init(0)))
        return fail(FORMA_THORVG_ENGINE_FAILED, "ThorVG engine initialization failed.");
    clear_error();
    return FORMA_THORVG_SUCCESS;
}

FormaThorvgResult forma_thorvg_terminate(void)
{
    if (!succeeded(tvg::Initializer::term()))
        return fail(FORMA_THORVG_ENGINE_FAILED, "ThorVG engine termination failed.");
    clear_error();
    return FORMA_THORVG_SUCCESS;
}

FormaThorvgResult forma_thorvg_document_create(
    const uint8_t* svg,
    size_t svg_size,
    FormaThorvgDocument** document)
{
    if (!svg || svg_size == 0 || !document || svg_size > std::numeric_limits<uint32_t>::max()) {
        return fail(FORMA_THORVG_INVALID_ARGUMENT, "SVG source and output document must be non-null and bounded.");
    }

    *document = nullptr;
    auto* picture = tvg::Picture::gen();
    if (!picture) return fail(FORMA_THORVG_OUT_OF_MEMORY, "ThorVG could not allocate an SVG picture.");

    if (!succeeded(picture->load(
            reinterpret_cast<const char*>(svg),
            static_cast<uint32_t>(svg_size),
            "image/svg+xml",
            nullptr,
            true))) {
        tvg::Paint::rel(picture);
        return fail(FORMA_THORVG_PARSE_FAILED, "ThorVG rejected the in-memory SVG source.");
    }

    auto* created = static_cast<FormaThorvgDocument*>(std::malloc(sizeof(FormaThorvgDocument)));
    if (!created) {
        tvg::Paint::rel(picture);
        return fail(FORMA_THORVG_OUT_OF_MEMORY, "Forma could not allocate an SVG document handle.");
    }
    created->picture = picture;

    *document = created;
    clear_error();
    return FORMA_THORVG_SUCCESS;
}

void forma_thorvg_document_destroy(FormaThorvgDocument* document)
{
    if (!document) return;
    tvg::Paint::rel(document->picture);
    std::free(document);
}

FormaThorvgResult forma_thorvg_document_size(
    const FormaThorvgDocument* document,
    float* width,
    float* height)
{
    if (!document || !width || !height)
        return fail(FORMA_THORVG_INVALID_ARGUMENT, "Document and size outputs must be non-null.");
    if (!succeeded(document->picture->size(width, height)))
        return fail(FORMA_THORVG_PARSE_FAILED, "ThorVG did not provide finite SVG dimensions.");
    clear_error();
    return FORMA_THORVG_SUCCESS;
}

FormaThorvgResult forma_thorvg_document_rasterize(
    const FormaThorvgDocument* document,
    uint32_t width,
    uint32_t height,
    uint8_t* rgba,
    size_t rgba_size)
{
    if (!document || !rgba || width == 0 || height == 0) {
        return fail(FORMA_THORVG_INVALID_ARGUMENT, "Document, output buffer, width, and height must be valid.");
    }

    const size_t pixel_count = static_cast<size_t>(width) * height;
    if (pixel_count > std::numeric_limits<size_t>::max() / 4 || rgba_size != pixel_count * 4) {
        return fail(FORMA_THORVG_INVALID_ARGUMENT, "Output buffer must contain exactly width * height * 4 bytes.");
    }

    auto* canvas = tvg::SwCanvas::gen();
    auto* paint = document->picture->duplicate();
    if (!canvas || !paint) {
        delete canvas;
        tvg::Paint::rel(paint);
        return fail(FORMA_THORVG_OUT_OF_MEMORY, "ThorVG could not allocate a raster canvas or picture.");
    }

    auto* picture = static_cast<tvg::Picture*>(paint);
    if (!succeeded(picture->size(static_cast<float>(width), static_cast<float>(height))) ||
        !succeeded(canvas->target(
            reinterpret_cast<uint32_t*>(rgba),
            width,
            width,
            height,
            tvg::ColorSpace::ABGR8888))) {
        tvg::Paint::rel(picture);
        delete canvas;
        return fail(FORMA_THORVG_RENDER_FAILED, "ThorVG rejected the exact-size premultiplied RGBA target.");
    }
    if (!succeeded(canvas->add(picture))) {
        tvg::Paint::rel(picture);
        delete canvas;
        return fail(FORMA_THORVG_RENDER_FAILED, "ThorVG could not add the SVG picture to the canvas.");
    }
    if (!succeeded(canvas->draw(true)) || !succeeded(canvas->sync())) {
        delete canvas;
        return fail(FORMA_THORVG_RENDER_FAILED, "ThorVG failed while drawing or synchronizing the SVG picture.");
    }

    delete canvas;
    clear_error();
    return FORMA_THORVG_SUCCESS;
}