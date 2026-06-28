shader_type canvas_item;

uniform float depth : hint_range(0.0, 1.0) = 0.0;
uniform vec4 water_color : source_color = vec4(0.0, 0.15, 0.3, 1.0);
uniform float wave_strength : hint_range(0.0, 0.05) = 0.008;
uniform float wave_speed : hint_range(0.0, 5.0) = 1.5;

// Absorption riêng theo kênh màu (Beer-Lambert): đỏ mất nhanh nhất, lam giữ lâu nhất
uniform vec3 absorption_coeff = vec3(0.9, 0.35, 0.12);
// Giảm tiếp lần 2: overlay này dễ làm phẳng màu vật thể gần camera nếu set quá cao,
// chỉ nên là lớp ambient mood rất nhẹ, gradient chính phải do WorldEnvironment fog đảm nhiệm
uniform float max_tint : hint_range(0.0, 1.0) = 0.15;

void fragment() {
    // Distortion — méo nhẹ như nhìn qua nước
    vec2 uv = UV;
    uv.x += sin(uv.y * 20.0 + TIME * wave_speed) * wave_strength * depth;
    uv.y += cos(uv.x * 20.0 + TIME * wave_speed) * wave_strength * depth;

    vec4 tex = texture(TEXTURE, uv);

    // Màu nước dịch theo absorption riêng từng kênh khi player xuống sâu hơn,
    // thay vì chỉ tối dần đều 1 màu cố định
    vec3 transmittance = exp(-absorption_coeff * depth);
    vec3 shifted_water_color = mix(water_color.rgb, water_color.rgb * transmittance, 0.5);

    float tint_amount = depth * max_tint;
    COLOR.rgb = mix(tex.rgb, shifted_water_color, tint_amount);
    COLOR.a = tint_amount;
}