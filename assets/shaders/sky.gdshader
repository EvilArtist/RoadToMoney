shader_type sky;

uniform vec3 sky_top_color : source_color = vec3(0.25, 0.55, 0.85);
uniform vec3 sky_horizon_color : source_color = vec3(0.55, 0.72, 0.85);
uniform vec3 ground_horizon_color : source_color = vec3(0.55, 0.72, 0.85);
uniform vec3 ground_bottom_color : source_color = vec3(0.1, 0.1, 0.12);

uniform float cloud_coverage  : hint_range(0.0, 1.0) = 0.0;
uniform float cloud_white_mix : hint_range(0.0, 1.0) = 1.0; // 1 = trắng (Trưa), 0 = đen+tint (Sáng/Chiều)
uniform float cloud_speed     : hint_range(0.0, 0.05) = 0.01;
uniform vec3  cloud_color_day  : source_color = vec3(1.0, 1.0, 1.0);
uniform vec3  cloud_color_dark : source_color = vec3(0.08, 0.08, 0.10);
uniform vec3  cloud_sun_tint   : source_color = vec3(1.0, 0.55, 0.2); // vàng-cam nhuốm phía mặt trời
uniform float cloud_sun_tint_strength : hint_range(0.0, 2.0) = 1.2;

uniform float star_visibility : hint_range(0.0, 1.0) = 0.0; // 0 = ngày, 1 = tối hẳn

// ── Noise cho mây (FBM) ──────────────────────────────────────────────
float hash21(vec2 p) {
	p = fract(p * vec2(127.1, 311.7));
	p += dot(p, p + 19.19);
	return fract(p.x * p.y);
}
float noise2(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float a = hash21(i);
	float b = hash21(i + vec2(1.0, 0.0));
	float c = hash21(i + vec2(0.0, 1.0));
	float d = hash21(i + vec2(1.0, 1.0));
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}
float fbm(vec2 p) {
	float v = 0.0, amp = 0.5;
	for (int i = 0; i < 4; i++) {
		v += amp * noise2(p);
		p *= 2.0;
		amp *= 0.5;
	}
	return v;
}

// ── Hash cho sao ──────────────────────────────────────────────────────
float star_hash(vec3 p) {
	return fract(sin(dot(p, vec3(12.9898, 78.233, 45.164))) * 43758.5453);
}

void sky() {
	float horizon_blend = pow(1.0 - max(EYEDIR.y, 0.0), 4.0);
	vec3 sky_col    = mix(sky_top_color, sky_horizon_color, horizon_blend);
	vec3 ground_col = mix(ground_horizon_color, ground_bottom_color, pow(1.0 - max(-EYEDIR.y, 0.0), 4.0));
	vec3 col = EYEDIR.y > 0.0 ? sky_col : ground_col;

	// ── Mây ──────────────────────────────────────────────────────────
	if (EYEDIR.y > 0.02 && cloud_coverage > 0.001) {
		vec2 cloud_uv = EYEDIR.xz / max(EYEDIR.y, 0.08) * 0.15 + vec2(TIME * cloud_speed, 0.0);
		float cloud_n = fbm(cloud_uv);
		cloud_n = pow(cloud_n, 0.75); // tăng tương phản — cụm mây rõ hơn, không loang đều

		// Mask rộng hơn bản cũ → mây phủ rõ thấy ngay ở coverage thấp hơn
		float cloud_mask = smoothstep(1.0 - cloud_coverage, 1.0 - cloud_coverage * 0.35, cloud_n);

		// Tint theo hướng mặt trời — phần mây quay về phía sun rực vàng/cam, phần còn lại tối
		float sun_facing = 0.0;
		if (LIGHT0_ENABLED) {
			vec2 eye_xz = normalize(EYEDIR.xz + 1e-5);
			vec2 sun_xz = normalize(LIGHT0_DIRECTION.xz + 1e-5);
			sun_facing = clamp(dot(eye_xz, sun_xz), 0.0, 1.0);
		}
		vec3 dark_tinted = mix(cloud_color_dark, cloud_sun_tint,
			pow(sun_facing, 2.5) * cloud_sun_tint_strength);
		vec3 cloud_final = mix(dark_tinted, cloud_color_day, cloud_white_mix);

		col = mix(col, cloud_final, cloud_mask * 0.92 * smoothstep(0.0, 0.1, EYEDIR.y));
	}

	// ── Sao ──────────────────────────────────────────────────────────
	if (star_visibility > 0.001 && EYEDIR.y > 0.0) {
		vec3 star_p = floor(EYEDIR * 220.0);
		float star_n = star_hash(star_p);
		float twinkle = 0.6 + 0.4 * sin(TIME * 3.0 + star_n * 50.0);
		float star_mask = step(0.997, star_n) * twinkle;
		col += vec3(star_mask) * star_visibility;
	}

	// ── Sun/Moon disc — dùng LIGHT0/LIGHT1 built-in, thay hẳn cơ chế
	// SkyMode LightOnly/LightAndSky cũ (xử lý "quả cầu đen" tận gốc) ──
	if (LIGHT0_ENABLED) {
		float sun_dot  = dot(EYEDIR, LIGHT0_DIRECTION);
		float sun_disc = smoothstep(0.9994, 0.9998, sun_dot);
		col += LIGHT0_COLOR * LIGHT0_ENERGY * sun_disc * 1.5;
	}
	if (LIGHT1_ENABLED) {
		float moon_dot  = dot(EYEDIR, LIGHT1_DIRECTION);
		float moon_disc = smoothstep(0.9990, 0.9998, moon_dot);
		// sàn năng lượng riêng cho disc — không phụ thuộc LIGHT1_ENERGY thật
		// (cái đó dùng cho lighting scene, ở đây chỉ cần trăng LUÔN hiện rõ, luôn tròn)
		col += LIGHT1_COLOR * max(LIGHT1_ENERGY, 0.6) * moon_disc;
	}

	COLOR = col;
}