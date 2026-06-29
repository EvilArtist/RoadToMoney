shader_type canvas_item;

// Dùng cho icon SeaResource chưa được khám phá trong BookScreen (#12).
// Desaturate hoàn toàn + tối màu để tạo hiệu ứng "bóng đen" (silhouette),
// giữ alpha gốc của texture để không vẽ ra ngoài hình dạng icon.

uniform float darken_amount : hint_range(0.0, 1.0) = 0.85;

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	float gray = dot(tex.rgb, vec3(0.299, 0.587, 0.114));
	vec3 silhouette = vec3(gray) * (1.0 - darken_amount);
	COLOR = vec4(silhouette, tex.a);
}
