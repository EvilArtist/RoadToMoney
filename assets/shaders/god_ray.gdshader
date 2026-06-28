shader_type spatial;
render_mode unshaded, cull_disabled, blend_add, depth_draw_never, depth_test_disabled;

// ── Appearance ───────────────────────────────────────────────
uniform vec4  ray_color   : source_color = vec4(0.55, 0.82, 1.0, 1.0);
uniform float ray_alpha   : hint_range(0.0, 1.0) = 0.18;

// ── Animation ────────────────────────────────────────────────
uniform float scroll_speed : hint_range(0.0, 1.0) = 0.06;   // how fast noise moves down
uniform float flicker_speed: hint_range(0.0, 4.0) = 0.9;    // brightness pulse rate
uniform float flicker_amp  : hint_range(0.0, 1.0) = 0.35;   // 0 = steady, 1 = wild

// ── Shape ────────────────────────────────────────────────────
uniform float edge_softness : hint_range(0.5, 8.0) = 2.5;   // lateral falloff sharpness
uniform float top_fade      : hint_range(0.0, 1.0) = 0.18;  // fade near ocean surface
uniform float bottom_fade   : hint_range(0.0, 1.0) = 0.45;  // fade near seabed

// ── Noise texture (assign a NoiseTexture2D in inspector) ─────
uniform sampler2D noise_tex : filter_linear, repeat_enable;
uniform float noise_scale   : hint_range(0.1, 4.0) = 1.2;
uniform float noise_contrast: hint_range(1.0, 6.0) = 2.8;   // punch the noise up

// ── Depth-based fade (camera distance) ───────────────────────
uniform float max_visibility : hint_range(5.0, 80.0) = 30.0; // fade fully at this dist

void fragment() {
    // UV.x = lateral position (0=left edge, 0.5=center, 1=right edge)
    // UV.y = vertical position (0=top/surface, 1=bottom/seabed)
    vec2 uv = UV;

    // ── 1. Lateral soft-edge mask ─────────────────────────────
    // remap so center = 1, edges = 0, then pow for softness
    float lateral = 1.0 - abs(uv.x * 2.0 - 1.0);
    lateral = pow(lateral, edge_softness);

    // ── 2. Vertical fade ──────────────────────────────────────
    float vfade_top    = smoothstep(0.0, top_fade,    uv.y);
    float vfade_bottom = smoothstep(1.0, 1.0 - bottom_fade, uv.y);
    float vfade = vfade_top * vfade_bottom;

    // ── 3. Animated noise ─────────────────────────────────────
    vec2 noise_uv = vec2(uv.x * noise_scale,
                         uv.y * noise_scale - TIME * scroll_speed);
    float noise = texture(noise_tex, noise_uv).r;

    // boost contrast: push mid-grays toward black/white
    noise = clamp((noise - 0.5) * noise_contrast + 0.5, 0.0, 1.0);

    // ── 4. Time flicker ───────────────────────────────────────
    float flicker = 1.0 - flicker_amp * (sin(TIME * flicker_speed + uv.x * 3.14) * 0.5 + 0.5);

    // ── 5. Distance fade (tính ở vertex(), bake vào COLOR.a) ──
    float depth_fade = COLOR.a;

    // ── 6. Combine ───────────────────────────────────────────
    float alpha = lateral * vfade * noise * flicker * depth_fade * ray_alpha;

    ALBEDO = ray_color.rgb;
    ALPHA  = alpha;
}

void vertex() {
    // Per-vertex depth fade using actual camera distance
    float dist = length(VERTEX - (inverse(MODELVIEW_MATRIX) * vec4(0,0,0,1)).xyz);
    float depth_fade = clamp(1.0 - dist / max_visibility, 0.0, 1.0);
    // Bake into vertex color alpha channel (read in fragment via COLOR.a)
    COLOR = vec4(1.0, 1.0, 1.0, depth_fade);
}
