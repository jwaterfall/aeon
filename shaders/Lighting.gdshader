shader_type spatial;

varying vec3 world_position;
varying vec3 normalized_normal;

uniform int chunk_lighting_data[295936]; // Expanded lighting data for neighboring chunks
uniform ivec2 chunk_position;
uniform ivec3 chunk_dimensions;

// Texture parameters
uniform sampler2D texture_sampler : filter_nearest, source_color;
uniform ivec2 texture_scale;

uniform float ambient_light = 0.1;

// Utility function to get the index in the lighting array
int get_index(ivec3 chunk_block_position) {
    ivec3 offset_position = chunk_block_position + ivec3(1, 0, 1);
    return (offset_position.y * (chunk_dimensions.z + 2) * (chunk_dimensions.x + 2)) + (offset_position.z * (chunk_dimensions.x + 2)) + offset_position.x;
}

// Utility function to get the light level of a block
float get_light_level(ivec3 chunk_block_position) {
    int index = get_index(chunk_block_position);
    return float(chunk_lighting_data[index]) / 15.0;
}

// Function to get the neighboring block's position based on the face normal
ivec3 get_neighbor_block_position(ivec3 chunk_block_position, vec3 normal) {
    ivec3 neighbor_position = chunk_block_position;
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
    // Sample texture color
    ivec3 chunk_world_position = ivec3(chunk_position.x * chunk_dimensions.x, 0, chunk_position.y * chunk_dimensions.z);

    vec3 texture_color = texture(texture_sampler, UV).rgb;

    vec3 offset_world_position = world_position + (normalized_normal * -0.01);
    ivec3 world_block_position = ivec3(floor(offset_world_position));

    // Convert vertex position to chunk-relative position
    ivec3 chunk_block_position = world_block_position - chunk_world_position;

    // Use normal to get neighboring block position
    ivec3 neighbor_block_position = get_neighbor_block_position(chunk_block_position, normalized_normal);

    // Get light level of the current block
    float light_level = get_light_level(neighbor_block_position);

    float multiplier = (1.0 - ambient_light) * light_level + ambient_light;

    // Apply light level to texture color
    vec3 final_color = mix(vec3(0.0), texture_color, multiplier);

    // Output final color
    ALBEDO = final_color;
}
