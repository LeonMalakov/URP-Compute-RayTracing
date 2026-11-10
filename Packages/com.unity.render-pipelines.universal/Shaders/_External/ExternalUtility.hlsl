#ifndef EXTERNAL_UTILITY_INCLUDED
#define EXTERNAL_UTILITY_INCLUDED

// Упаковка беззнакового целого 0..255 -> float (0..1)
float PackUInt8ToR8(uint v)
{
    v = v & 0xFFu;
    return (float)v / 255.0;
}

// Распаковка float (0..1) -> uint 0..255
uint UnpackR8ToUInt8(float f)
{
    f = saturate(f);
    // round к ближайшему целому и приводим к uint
    return (uint)round(f * 255.0);
}


#endif
