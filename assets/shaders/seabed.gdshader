shader_type spatial;
render_mode diffuse_burley, specular_disabled;

uniform vec4  sand_color  : source_color = vec4(0.55, 0.43, 0.29, 1.0);
uniform vec4  rock_color  : source_color = vec4(0.35, 0.30, 0.25, 1.0);
uniform float slope_strength : hint_range(0.0, 20.0) = 8.0;
uniform float noise_scale    : hint_range(0.0, 0.5)  = 0.12;
uniform float depth_darkening : hint_range(0.0, 1.0) = 0.4;

uniform float seam_blend_width     : hint_range(0.0, 100.0) = 40.0;
uniform float seam_smooth_strength : hint_range(0.0, 1.0)   = 0.85; 
uniform float seam_local_z         : hint_range(-400.0, 400.0) = -200.0; 

uniform float caustics_strength : hint_range(0.0, 1.0) = 0.5;
uniform float caustics_scale    : hint_range(0.01, 1.0) = 0.1;
uniform float caustics_speed    : hint_range(0.0, 1.0)  = 0.25;

// Color variation: 2 tone cát lerp theo noise lớn → tránh màu phẳng đều
uniform vec3  sand_color_b       : source_color = vec3(0.60, 0.50, 0.35);  // sáng hơn, gần với sand_color_a hơn
uniform float color_variation    : hint_range(0.0, 1.0) = 0.30;            // giảm từ 0.55 — variation nhẹ hơn, không tạo patch tối
uniform float color_noise_scale  : hint_range(0.001, 0.05) = 0.008;

// Normal map procedural: gradient của hàm height noise → sand ripple không cần texture
uniform float ripple_strength    : hint_range(0.0, 2.0) = 0.25;            // giảm từ 0.5 — blob sáng/tối quá mạnh
uniform float ripple_scale       : hint_range(0.01, 1.0) = 0.08;

// Caustics procedural: 3 lớp sóng sin XOAY theo góc khác nhau rồi cộng lại.
// (sin(x)*sin(y) đơn giản sẽ luôn tạo lưới vuông vì 2 trục độc lập —
// phải xoay từng lớp theo góc khác nhau mới phá được tính lưới, giống
// kỹ thuật caustics chuẩn dùng nhiều lớp sóng phẳng giao thoa lệch góc)
float caustics_wave(vec2 p, float angle_rad, float freq, float speed, float t) {
    float s = sin(angle_rad);
    float c = cos(angle_rad);
    vec2 rp = vec2(p.x * c - p.y * s, p.x * s + p.y * c);
    return sin(rp.x * freq + t * speed);
}

float caustics_pattern(vec2 p, float t) {
    // domain warp nhẹ để tránh lặp pattern đều quá rõ
    p += 0.25 * vec2(sin(p.y * 0.6 + t * 0.3), cos(p.x * 0.6 - t * 0.25));

    float w1 = caustics_wave(p, 0.4,  5.0,  1.0, t);
    float w2 = caustics_wave(p, 2.3,  6.0, -0.7, t);
    float w3 = caustics_wave(p, 4.1,  4.5,  0.8, t);

    float v = (w1 + w2 + w3) / 3.0;       // gộp 3 lớp, range ~[-1,1]
    v = clamp(v * 0.5 + 0.5, 0.0, 1.0);   // remap về [0,1]
    return pow(v, 4.0);                    // nén thành các vệt sáng nhỏ, nền tối hơn
}

// Hash noise mượt hơn fract(sin()) — dùng cho color variation scale lớn, không lặp pattern rõ
float hash21(vec2 p) {
    p = fract(p * vec2(127.1, 311.7));
    p += dot(p, p + 19.19);
    return fract(p.x * p.y);
}

float smooth_noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + vec2(1.0, 0.0));
    float c2 = hash21(i + vec2(0.0, 1.0));
    float d = hash21(i + vec2(1.0, 1.0));
    return mix(mix(a, b, f.x), mix(c2, d, f.x), f.y);
}

// Sand ripple normal: finite difference của smooth_noise (không dùng sine axes thẳng góc
// vì sin(x)+sin(y) luôn tạo lưới vuông — noise cho kết quả hữu cơ, không lặp pattern rõ)
vec3 ripple_normal(vec2 p, float scale, float strength) {
    float eps = 0.8;
    // 2 octave noise cho gợn cát: octave lớn = undulation rộng, octave nhỏ = ripple mịn
    float hC = smooth_noise(p * scale)        * 0.7
             + smooth_noise(p * scale * 2.3 + vec2(4.7, 1.3)) * 0.3;
    float hX = smooth_noise((p + vec2(eps, 0.0)) * scale)        * 0.7
             + smooth_noise((p + vec2(eps, 0.0)) * scale * 2.3 + vec2(4.7, 1.3)) * 0.3;
    float hZ = smooth_noise((p + vec2(0.0, eps)) * scale)        * 0.7
             + smooth_noise((p + vec2(0.0, eps)) * scale * 2.3 + vec2(4.7, 1.3)) * 0.3;
    float dX = (hX - hC) / eps * strength;
    float dZ = (hZ - hC) / eps * strength;
    return normalize(vec3(-dX, 1.0, -dZ));
}

void vertex() {
    float wave = sin(VERTEX.x * 0.08) * cos(VERTEX.z * 0.06) * 0.3
               + sin(VERTEX.x * 0.2  + VERTEX.z * 0.15) * 0.1;

    VERTEX.y += wave;
}

void fragment() {
    float depth = clamp(-VERTEX.y / 80.0, 0.0, 1.0);
    vec4 base = mix(sand_color, rock_color, depth * depth_darkening);

    // Color variation: smooth noise scale lớn (VERTEX.xz thay vì UV để không lặp pattern rõ)
    float cv = smooth_noise(VERTEX.xz * color_noise_scale);
    base.rgb = mix(base.rgb, sand_color_b, cv * color_variation);

    // High-freq noise nhỏ giữ lại để texture vi mô
    float noise = fract(sin(dot(UV, vec2(12.9898, 78.233))) * 43758.5);
    base.rgb += (noise - 0.5) * noise_scale * 0.5;  // giảm từ 1.0 xuống 0.5 vì color variation đã làm nhiệm vụ này

    // Caustics: chỉ rõ ở vùng gần mặt nước (depth thấp), fade dần theo độ sâu
    float caustic = caustics_pattern(VERTEX.xz * caustics_scale, TIME * caustics_speed);
    float caustic_fade = 1.0 - depth;
    base.rgb += caustic * caustic_fade * caustics_strength;

    ALBEDO    = base.rgb;
    ROUGHNESS = 0.95;
    METALLIC  = 0.0;

    // Sand ripple normal: chỉ áp dụng ở vùng cát nông (depth thấp), fade dần khi sâu
    vec3 ripple = ripple_normal(VERTEX.xz, ripple_scale, ripple_strength);
    float ripple_fade = clamp(1.0 - depth * 3.0, 0.0, 1.0);  // rõ ở shallow, mờ ở deep
    NORMAL_MAP = ripple * 0.5 + 0.5;
    NORMAL_MAP_DEPTH = ripple_strength * ripple_fade;

    float distToSeam  = abs(VERTEX.z - seam_local_z);
    float blendFactor = 1.0 - smoothstep(0.0, seam_blend_width, distToSeam);
    blendFactor *= seam_smooth_strength;

    vec3 worldUp    = vec3(0.0, 1.0, 0.0);
    vec3 fakeNormal = normalize((VIEW_MATRIX * vec4(worldUp, 0.0)).xyz);

    NORMAL = normalize(mix(NORMAL, fakeNormal, blendFactor));
}
