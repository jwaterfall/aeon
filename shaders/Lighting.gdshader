shader_type spatial;


uniform int chunk_lighting_data[82944]; // x - (16 + 2) * y - 256 * z - (16 + 2)
uniform ivec2 chunk_position;
uniform ivec3 chunk_dimensions;
uniform sampler2D texture_sampler : filter_nearest, source_color;
uniform ivec2 texture_scale;
uniform float ambient_light = 0.1;

varying vec3 world_position;
varying vec3 normalized_normal;

int get_light_index(ivec3 local_position) {
    ivec3 offset_position = local_position + ivec3(1, 0, 1); // Adjust for extra margin in lighting data
    return (offset_position.y * (chunk_dimensions.z + 2) * (chunk_dimensions.x + 2)) + 
           (offset_position.z * (chunk_dimensions.x + 2)) + 
           offset_position.x;
}

float get_light_level(ivec3 local_position) {
    int index = get_light_index(local_position);
    return float(chunk_lighting_data[index]) / 15.0;
}

ivec3 get_neighbor_local_position(ivec3 local_position, vec3 normal) {
    ivec3 neighbor_position = local_position;

    if (normal.x > 0.5) neighbor_position.x += 1;  // Right face
    else if (normal.x < -0.5) neighbor_position.x -= 1;  // Left face

    if (normal.y > 0.5) neighbor_position.y += 1;  // Top face
    else if (normal.y < -0.5) neighbor_position.y -= 1;  // Bottom face

    if (normal.z > 0.5) neighbor_position.z += 1;  // Front face
    else if (normal.z < -0.5) neighbor_position.z -= 1;  // Back face

    return neighbor_position;
}

void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    normalized_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
    ivec3 chunk_world_position = ivec3(chunk_position.x * chunk_dimensions.x, 0, chunk_position.y * chunk_dimensions.z);
    vec3 offset_world_position = world_position + (normalized_normal * -0.01); // Offset slightly to prevent z-fighting
    ivec3 block_world_position = ivec3(floor(offset_world_position));

    ivec3 block_local_position = block_world_position - chunk_world_position;
    ivec3 neighbor_local_position = get_neighbor_local_position(block_local_position, normalized_normal);

    float light_level = get_light_level(neighbor_local_position);
    float light_multiplier = mix(ambient_light, 1.0, light_level);

    vec3 texture_color = texture(texture_sampler, UV).rgb;
    ALBEDO = texture_color * light_multiplier;
}
