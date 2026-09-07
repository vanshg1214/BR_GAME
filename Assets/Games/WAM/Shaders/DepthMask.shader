Shader "Custom/DepthMask" {
    SubShader {
        // Render AFTER the Skybox (Transparent-10)
        // This ensures the skybox is fully drawn before the mask writes its invisible depth, fixing the smearing!
        Tags { "RenderType"="Opaque" "Queue"="Transparent-10" }
        
        // Don't draw any color to the screen
        ColorMask 0
        
        // DO write to the depth buffer (Z-buffer)
        ZWrite On
        
        // Render both sides of the mesh just to be safe
        Cull Off
        
        Pass {}
    }
}
