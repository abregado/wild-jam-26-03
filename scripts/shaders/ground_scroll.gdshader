shader_type spatial;

uniform float scroll_speed : hint_range(0.0, 200.0) = 10.0;
uniform vec4 base_color : source_color = vec4(0.76, 0.60, 0.38, 1.0);
uniform vec4 detail_color : source_color = vec4(0.62, 0.48, 0.28, 1.0);
uniform float tile_size : hint_range(1.0, 200.0) = 40.0;

void fragment() {
	ALBEDO = base_color.rgb;
	ROUGHNESS = 0.95;
	METALLIC = 0.0;
}
