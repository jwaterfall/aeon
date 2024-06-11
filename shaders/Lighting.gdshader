shader_type spatial;

uniform sampler2D texture_sampler : filter_nearest, source_color;
uniform vec3 ambient_light = vec3(0.1);

varying vec4 vertex_color;

void vertex() {
    vertex_color = COLOR;
}

void fragment() {
    vec3 texture_color = texture(texture_sampler, UV).rgb;
    vec3 light_level = max(vertex_color.rgb, ambient_light);
    ALBEDO = texture_color * light_level;
}