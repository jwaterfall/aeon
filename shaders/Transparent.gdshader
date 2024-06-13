shader_type spatial;

render_mode depth_prepass_alpha;

uniform sampler2D texture_sampler : filter_nearest, source_color;
uniform vec3 ambient_light = vec3(0.1);

varying vec4 vertex_color;

void vertex() {
    vertex_color = COLOR;
}

void fragment() {
    vec4 texture_color = texture(texture_sampler, UV);
    vec3 light_level = max(vertex_color.rgb, ambient_light);
    ALBEDO = texture_color.rgb * light_level;
	ALPHA *= texture_color.a;
}