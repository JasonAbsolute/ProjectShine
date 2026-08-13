shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_lambert, specular_schlick_ggx;

uniform sampler2D albedo_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform vec4 hat_color : source_color = vec4(0.8, 0.1, 0.1, 1.0);      // red cap / shirt
uniform vec4 overalls_color : source_color = vec4(0.1, 0.2, 0.8, 1.0); // blue overalls
uniform vec4 skin_color : source_color = vec4(0.95, 0.72, 0.58, 1.0);  // skin tone
uniform float match_threshold = 0.28; // how close a pixel must be to trigger swap
uniform float blend_strength = 1.0;  // 0=no swap, 1=full swap

// Reference colors sampled from the original SMS Mario texture atlas
const vec3 REF_RED  = vec3(0.776, 0.098, 0.082); // hat / shirt red
const vec3 REF_BLUE = vec3(0.094, 0.133, 0.522); // overalls blue
const vec3 REF_SKIN = vec3(0.918, 0.694, 0.518); // skin peach

void fragment() {
	vec4 tex = texture(albedo_texture, UV);

	vec3 c = tex.rgb;

	// Distance to each reference color in RGB space
	float d_red  = length(c - REF_RED);
	float d_blue = length(c - REF_BLUE);
	float d_skin = length(c - REF_SKIN);

	// Softly blend toward replacement color when close enough
	float t_red  = clamp(1.0 - d_red  / match_threshold, 0.0, 1.0);
	float t_blue = clamp(1.0 - d_blue / match_threshold, 0.0, 1.0);
	float t_skin = clamp(1.0 - d_skin / match_threshold, 0.0, 1.0);

	// Only one swap fires at a time — whichever is the strongest match wins
	vec3 result = c;
	result = mix(result, hat_color.rgb      * (c / max(REF_RED,  vec3(0.001))), t_red  * blend_strength);
	result = mix(result, overalls_color.rgb * (c / max(REF_BLUE, vec3(0.001))), t_blue * blend_strength);
	result = mix(result, skin_color.rgb     * (c / max(REF_SKIN, vec3(0.001))), t_skin * blend_strength);

	ALBEDO = result;
	ALPHA  = tex.a;
}
