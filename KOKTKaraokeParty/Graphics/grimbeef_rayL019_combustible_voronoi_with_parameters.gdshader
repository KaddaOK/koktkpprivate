shader_type canvas_item;

#define iTime TIME
#define iResolution 1.0/SCREEN_PIXEL_SIZE
#define fragColor COLOR
#define fragCoord FRAGCOORD

uniform float kelvin : hint_range(0.0, 10000.0) = 1000.0;
uniform float red_colour: hint_range(0.0, 24.0, 0.1) = 7.4;
uniform float green_colour: hint_range(0.0, 24.0, 0.1) = 5.6;
uniform float blue_colour: hint_range(0.0, 24.0, 0.1) = 4.4;
uniform float time_factor: hint_range(0.0, 3.0, 0.01) = 1.5;
uniform bool move = false;
uniform float resolution: hint_range(0.01, 3.0, 0.01) = 0.4;
uniform float sin_roll: hint_range(0.0, 1.0, 0.01) = 0.25;
uniform float cos_roll: hint_range(0.0, 1.0, 0.01) = 0.25;
uniform float alpha: hint_range(0.0, 1.0, 0.01) = 1.0;
/* 

    Combustible Voronoi Layers
	--------------------------

    The effect itself is nothing new or exciting, just some moving 3D Voronoi layering. 
    However, the fire palette might prove useful to some.

*/


// This is my favorite fire palette. It's trimmed down for shader usage, and is based on an 
// article I read at Hugo Elias's site years ago. I'm sure most old people, like me, have 
// visited his site at one time or another:
//
// http://freespace.virgin.net/hugo.elias/models/m_ffire.htm
//
vec3 firePalette(float i){

    float T = 1400. + kelvin*i; // Temperature range (in Kelvin).
	//vec4(0.1,0.1,0.1,1.0
    vec3 L =  vec3(red_colour, blue_colour, green_colour); // Red, green, blue wavelengths (in hundreds of nanometers).
    L = pow(L,vec3(5)) * (exp(1.43876719683e5/(T*L)) - 1.);
    return 1. - exp(-5e8/L); // Exposure level. Set to "50." For "70," change the "5" to a "7," etc.
}

/*
vec3 firePalette(float i){

    float T = 1400. + 1300.*i; // Temperature range (in Kelvin).
    // Hardcode red, green and blue wavelengths (in hundreds of nanometers).
    vec3 L = (exp(vec3(19442.7999572, 25692.271372, 32699.2544734)/T) - 1.);
    // Exposure level. Set to "50" For "70," change the ".5" to a ".7," etc.
    return 1. - exp(-vec3(22532.6051122, 90788.296915, 303184.239775)*2.*.5/L); 
}
*/

// Hash function. This particular one probably doesn't disperse things quite as nicely as some 
// of the others around, but it's compact, and seems to work.
//
vec3 hash33(vec3 p){ 
    
    float n = sin(dot(p, vec3(7, 157, 113)));   //float n = sin(dot(p, vec3(7, 157, 113)));  
    return fract(vec3(2097152, 262144, 32768)*n); 
}

// 3D Voronoi: Obviously, this is just a rehash of IQ's original.
//
float voronoi(vec3 p){

	vec3 b, r, g = floor(p);
	p = fract(p); // "p -= g;" works on some GPUs, but not all, for some annoying reason.
	
	// Maximum value: I think outliers could get as high as "3," the squared diagonal length 
	// of the unit cube, with the mid point being "0.75." Is that right? Either way, for this 
	// example, the maximum is set to one, which would cover a good part of the range, whilst 
	// dispensing with the need to clamp the final result.
	float d = 1.; 
     
    // I've unrolled one of the loops. GPU architecture is a mystery to me, but I'm aware 
    // they're not fond of nesting, branching, etc. My laptop GPU seems to hate everything, 
    // including multiple loops. If it were a person, we wouldn't hang out. 
	for(int j = -1; j <= 1; j++) {
	    for(int i = -1; i <= 1; i++) {
    		
		    b = vec3(float(i), float(j), -1);
		    r = b - p + hash33(g+b);
		    d = min(d, dot(r,r));
    		
		    b.z = 0.0;
		    r = b - p + hash33(g+b);
		    d = min(d, dot(r,r));
    		
		    b.z = 1.;
		    r = b - p + hash33(g+b);
		    d = min(d, dot(r,r));
    			
	    }
	}
	
	return d; // Range: [0, 1]
}

// Standard fBm function with some time dialation to give a parallax 
// kind of effect. In other words, the position and time frequencies 
// are changed at different rates from layer to layer.
//
float noiseLayers(in vec3 p) {

    // Normally, you'd just add a time vector to "p," and be done with 
    // it. However, in this instance, time is added seperately so that 
    // its frequency can be changed at a different rate. "p.z" is thrown 
    // in there just to distort things a little more.
    vec3 t = vec3(0., 0., p.z + iTime*time_factor);

    const int iter = 5; // Just five layers is enough.
    float tot = 0., sum = 0., amp = 1.; // Total, sum, amplitude.

    for (int i = 0; i < iter; i++) {
        tot += voronoi(p + t) * amp; // Add the layer to the total.
        p *= 2.; // Position multiplied by two.
        t *= 1.5; // Time multiplied by less than two.
        sum += amp; // Sum of amplitudes.
        amp *= .5; // Decrease successive layer amplitude, as normal.
    }
    
    return tot/sum; // Range: [0, 1].
}

float rand(vec2 st) {
	
    return fract(sin(dot(st.xy, vec2(12.9898,72.233))) * 53758.5453123);
}


void fragment()
{
    // Screen coordinates.
	vec2 uv = (UV - 0.5) * vec2( SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x, 1.0);
	
	// Shifting the central position around, just a little, to simulate a 
	// moving camera, albeit a pretty lame one.
	if (move) {
		uv += vec2(sin(iTime*.5)*.25, cos(iTime*.5)*.125);
	}
	
	
    // Constructing the unit ray. 
	vec3 rd = normalize(vec3(uv.x, uv.y, resolution + 0.1415926535898/8.));

    // Rotating the ray about the XY plane, to simulate a rolling camera.
	
	float cs = cos(iTime*cos_roll), si = sin(iTime*sin_roll);
    // Apparently "r *= rM" can break in some older browsers.
	rd.xy = rd.xy*mat2(vec2(cs, -si), vec2(si, cs)); 
	
	// Passing a unit ray multiple into the Voronoi layer function, which 
	// is nothing more than an fBm setup with some time dialation.
	float c = noiseLayers(rd*2.);
	
	// Optional: Adding a bit of random noise for a subtle dust effect. 
	//c = max(c + dot(hash33(rd)*2. - 1., vec3(.015)), 0.);

    // Coloring:
    
    // Nebula.
    c *= sqrt(c)*1.5; // Contrast.
    vec3 col = firePalette(c); // Palettization.
    //col = mix(col, col.zyx*.1+ c*.9, clamp((1.+rd.x+rd.y)*0.45, 0., 1.)); // Color dispersion.
    col = mix(col, col.zyx*.15 + c*.85, min(pow(dot(rd.xy, rd.xy)*1.2, 1.5), 1.)); // Color dispersion.
    col = pow(col, vec3(1.25)); // Tweaking the contrast a little.
    
    // The fire palette on its own. Perhaps a little too much fire color.
    //c = pow(c*1.33, 1.25);
    //vec3 col =  firePalette(c);
   
    // Black and white, just to keep the art students happy. :)
	//c *= c*1.5;
	//vec3 col = vec3(c);
	
	// Rough gamma correction, and done.
	fragColor = (vec4(sqrt(clamp(col, 0., 1.)), 1))  * alpha;
	//fragColor.a =(fragColor.r + fragColor.g + fragColor.b) / 3.;
}