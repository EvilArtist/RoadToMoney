shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, diffuse_burley, specular_schlick_ggx;

uniform vec4  shallow_color : source_color = vec4(0.04, 0.28, 0.48, 0.88);
uniform vec4  deep_color    : source_color = vec4(0.02, 0.10, 0.22, 0.95);
uniform float wave_speed    : hint_range(0.0, 2.0)  = 0.5;
uniform float wave_scale    : hint_range(0.0, 0.5)  = 0.22;
uniform float fresnel_power : hint_range(1.0, 60.0) = 4.0;
uniform vec3 sky_tint = vec3(0.55, 0.72, 0.85);
uniform float view_depth : hint_range(0.0, 1.0) = 0.0;  
uniform float wave_choppiness : hint_range(0.0, 2.0) = 0.6;

void vertex()
{
    // Sóng lớn — lan rộng, chậm
    float bigWave = sin(VERTEX.x * 0.3 + TIME * wave_speed)
                   * cos(VERTEX.z * 0.2 + TIME * wave_speed * 0.7)
                   * wave_scale
                   + sin(VERTEX.x * 0.7 + VERTEX.z * 0.5 + TIME * wave_speed * 1.3)
                   * wave_scale * 0.4;

    // Sóng nhỏ — gợn lăn tăn, nhanh hơn, chồng lên sóng lớn
    float chopWave = sin(VERTEX.x * 1.8 + VERTEX.z * 1.4 + TIME * wave_speed * 2.2)
                    * cos(VERTEX.x * 1.1 - VERTEX.z * 1.6 + TIME * wave_speed * 1.7)
                    * wave_scale * 0.25 * wave_choppiness;

    VERTEX.y += bigWave + chopWave;
}

void fragment()
{
    vec3 normal = FRONT_FACING ? NORMAL : -NORMAL;
    float effective_power = mix(fresnel_power, 45.0, view_depth);
    // Fresnel — nhìn thẳng xuống = trong, nhìn ngang = đục
    float fresnel = pow(1.0 - clamp(dot(normal, VIEW), 0.0, 1.0), effective_power);
    fresnel = clamp(fresnel, 0.0, 0.92);
    vec4  color   = mix(shallow_color, deep_color, fresnel);
    vec3 water_color = mix( shallow_color.rgb, deep_color.rgb, fresnel );
    // Sky_tint giảm dần theo độ sâu — khi đã lặn đủ sâu, không còn pha màu trời sáng vào nữa
    vec3 dimmed_sky_tint = mix(sky_tint, vec3(0.0, 0.02, 0.05), view_depth);
    water_color = mix(water_color, dimmed_sky_tint, fresnel * 0.65);
    ALBEDO = water_color;

    // Alpha tăng dần lên gần 1.0 theo độ sâu — mặt nước đặc hẳn lại, không còn xuyên thấu
    float surface_alpha = mix( shallow_color.a, deep_color.a, fresnel );
    ALPHA = mix(surface_alpha, 1.0, view_depth);
    ROUGHNESS = 0.08;
    METALLIC  = 0.0;
    SPECULAR  = 1.0;

    // Normal map khớp tần số với sóng thật ở vertex(), cộng thêm lớp gợn nhỏ chi tiết
    float nx = cos(VERTEX.x * 0.3 + TIME * wave_speed) * 0.3 * wave_scale
             + sin(UV.x * 90.0 + TIME * 1.8) * 0.25 * wave_choppiness
             + sin(UV.x * 220.0 + UV.y * 180.0 + TIME * 2.5) * 0.10;

    float ny = -sin(VERTEX.z * 0.2 + TIME * wave_speed * 0.7) * 0.3 * wave_scale
             + cos(UV.y * 85.0 + TIME * 1.6) * 0.25 * wave_choppiness
             + cos(UV.x * 200.0 - UV.y * 190.0 + TIME * 2.3) * 0.10;
    // Glint lăn tăn giả lập — dùng VERTEX (mét thực) để ô lưới nhỏ cỡ ~0.4m, ra đốm chứ không ra dải
// Glint lăn tăn giả lập — mod() để giữ toạ độ luôn nhỏ, tránh mất precision ở mediump (GLES3)


    NORMAL_MAP = normalize(vec3(nx, ny, 1.0));
}