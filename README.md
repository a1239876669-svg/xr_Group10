# VR Apartment Viewing System — XR Group 10

A virtual-reality apartment viewing system designed for use with a Meta Quest headset. Users can walk
and teleport through a virtual apartment, measure doorways and corridor widths, and leave markers and
comments about room size, corridor comfort, and furniture layout, to support property presentation and
spatial evaluation.

## Group Members

| Name | Student ID | GitHub Account | Responsibility |
| --- | --- | --- | --- |
| Guo Jikang | 26125103g | YuzYue | Distance measurement and feedback |
| Fan Zujian | 26124914g | fanfanzujian-oss | User interface and onboarding tutorial |
| Yang Jiale | 26042226g | YJL-196 | Movement comfort |
| Zhuang Kaiyu | 26109544g | KaiZ755 | 3D modeling and shopping bag |
| Xu Boyan | 26052354g | a1239876669-svg | Integration, version control, and project setup |

## Implemented Features

- Floating world-space menu, onboarding tutorial, smooth locomotion and teleportation, safe reset point
- Door and furniture interaction, shopping bag grabbing with three sizes, collision feedback
- Movement comfort: motion vignette, smooth acceleration and deceleration, three presets (Comfort / Standard / Sensitive)
- Distance measurement and annotation: automatic room detection, automatic saving, and exportable records

## Project Information

- Engine version: Unity 6000.6.4f1
- XR platform: Meta Quest (OpenXR / Meta XR SDK)
- Integration branch: `merge-apartmentguidence` (the final deliverable is on this branch)
- Main scene: `Assets/Scenes/Apartment_Detailed_VR.unity`

## How to Open the Project

1. Install Unity Hub and Unity 6000.6.4f1.
2. In Unity Hub, choose Add → Add project from disk and select the repository root folder.
3. Open `Assets/Scenes/Apartment_Detailed_VR.unity` and click Play with a Meta Quest headset connected.
