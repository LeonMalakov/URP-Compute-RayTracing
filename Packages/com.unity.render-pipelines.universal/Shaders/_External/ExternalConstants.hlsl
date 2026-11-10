#ifndef EXTERNAL_CONSTANTS_INCLUDED
#define EXTERNAL_CONSTANTS_INCLUDED

#define MAX_CAMERAS 8 // Активная + 7 порталов. PortalMaskCamera здесь не считается.

#if UNITY_REVERSED_Z
   
#else

#endif

#if UNITY_REVERSED_Z
    #define PORTALS_DEPTH_NEAR_VALUE 1.0
    #define PORTALS_DEPTH_FAR_VALUE 0.0
    #define PORTALS_DEPTH_OFFSET 0.0015
#else
    #define PORTALS_DEPTH_NEAR_VALUE 0.0
    #define PORTALS_DEPTH_FAR_VALUE 1.0
    #define PORTALS_DEPTH_OFFSET -0.0015
#endif

#define PORTALS_DEPTH_OFFSET_COMP 0.002

#endif
