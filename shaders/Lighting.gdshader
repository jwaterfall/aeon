shader_type spatial;

uniform vec3 chunk_lighting_data[82944]; // x - (16 + 2) * y - 256 * z - (16 + 2)
uniform ivec2 chunk_position;
uniform ivec3 chunk_dimensions;
uniform sampler2D texture_sampler : filter_nearest, source_color;
uniform vec3 ambient_light = vec3(0.1);

varying vec3 world_position;
varying vec3 normalized_normal;

int get_light_index(ivec3 local_position) {
    ivec3 offset_position = local_position + ivec3(1, 0, 1); // Adjust for extra margin in lighting data
    return (offset_position.y * (chunk_dimensions.z + 2) * (chunk_dimensions.x + 2)) + 
           (offset_position.z * (chunk_dimensions.x + 2)) + 
           offset_position.x;
}

vec3 get_light_level(ivec3 local_position) {
    int index = get_light_index(local_position);
    return chunk_lighting_data[index] / 15.0; // Normalize the light level
}

vec3 get_offset_light_level(ivec3 local_position) {
    // Offset by 0.5 to get the light level at the center of the block
    vec3 light_000 = get_light_level(local_position);
    vec3 light_100 = get_light_level(local_position + ivec3(-1, 0, 0));
    vec3 light_010 = get_light_level(local_position + ivec3(0, -1, 0));
    vec3 light_110 = get_light_level(local_position + ivec3(-1, -1, 0));
    vec3 light_001 = get_light_level(local_position + ivec3(0, 0, -1));
    vec3 light_101 = get_light_level(local_position + ivec3(-1, 0, -1));
    vec3 light_011 = get_light_level(local_position + ivec3(0, -1, -1));
    vec3 light_111 = get_light_level(local_position + ivec3(-1, -1, -1));

    vec3 light_x00 = mix(light_100, light_000, 0.5);
    vec3 light_x10 = mix(light_110, light_010, 0.5);
    vec3 light_x01 = mix(light_101, light_001, 0.5);
    vec3 light_x11 = mix(light_111, light_011, 0.5);

    vec3 light_xy0 = mix(light_x10, light_x00, 0.5);
    vec3 light_xy1 = mix(light_x11, light_x01, 0.5);

    return mix(light_xy1, light_xy0, 0.5);
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

    // Fetch light levels for the corners of the voxel containing the fragment
    vec3 light_000 = get_offset_light_level(block_local_position);
    vec3 light_100 = get_offset_light_level(block_local_position + ivec3(1, 0, 0));
    vec3 light_010 = get_offset_light_level(block_local_position + ivec3(0, 1, 0));
    vec3 light_110 = get_offset_light_level(block_local_position + ivec3(1, 1, 0));
    vec3 light_001 = get_offset_light_level(block_local_position + ivec3(0, 0, 1));
    vec3 light_101 = get_offset_light_level(block_local_position + ivec3(1, 0, 1));
    vec3 light_011 = get_offset_light_level(block_local_position + ivec3(0, 1, 1));
    vec3 light_111 = get_offset_light_level(block_local_position + ivec3(1, 1, 1));

    // Interpolate the light levels based on the fragment's position within the block
    vec3 position_within_block = fract(offset_world_position);

    vec3 light_x00 = mix(light_000, light_100, position_within_block.x);
    vec3 light_x10 = mix(light_010, light_110, position_within_block.x);
    vec3 light_x01 = mix(light_001, light_101, position_within_block.x);
    vec3 light_x11 = mix(light_011, light_111, position_within_block.x);

    vec3 light_xy0 = mix(light_x00, light_x10, position_within_block.y);
    vec3 light_xy1 = mix(light_x01, light_x11, position_within_block.y);

    vec3 light_level = mix(light_xy0, light_xy1, position_within_block.z);
    light_level = max(light_level, ambient_light); // Ensure ambient light is at least the ambient light

    vec3 texture_color = texture(texture_sampler, UV).rgb;
    vec3 final_color = texture_color * light_level;
    ALBEDO = final_color;
}
