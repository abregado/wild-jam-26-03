shader_type spatial;

uniform float scroll_speed : hint_range(0.0, 200.0) = 10.0;
uniform vec4 base_color : source_color = vec4(0.76, 0.60, 0.38, 1.0);
uniform vec4 detail_color : source_color = vec4(0.62, 0.48, 0.28, 1.0);
uniform float tile_size : hint_range(1.0, 200.0) = 40.0;

void fragment() {
	vec2 uv = UV * tile_size;
	// Scroll in +Z direction (UV.y maps to world Z).
	// scroll_speed is in world units/sec; scale factor maps units to UV.
	uv.y += TIME * scroll_speed * 0.025;

	// Simple stripe/grid pattern as placeholder desert ground.
	float stripeX = step(0.92, fract(uv.x));
	float stripeY = step(0.92, fract(uv.y));
	float grid = max(stripeX, stripeY);

	ALBEDO = mix(base_color.rgb, detail_color.rgb, grid);
	ROUGHNESS = 0.95;
	METALLIC = 0.0;
}
