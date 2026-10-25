"""Check multiplayer asset ownership without opening or modifying Unity scenes."""
from pathlib import Path
import re
import sys
ROOT = Path(__file__).resolve().parents[1]
MASTER = 131072
DESTROY_ON_LEAVE = 262144
WORLD = [
    'Assets/02_Prefabs/GiantGolemMars.prefab',
    'Assets/02_Prefabs/GiantGolemNeptune.prefab',
    'Assets/Enemies/Reksai_Marte/Prefabs/Reksai_Fusion.prefab',
    'Assets/Enemies/GusanoExcavador/Gusano_Excavador_Fusion.prefab',
    'Assets/Enemies/JungleNightmare/Prefabs/JungleNightmare_Fusion.prefab',
]
errors = []
for relative in WORLD:
    text = (ROOT / relative).read_text(encoding='utf-8')
    flags = [int(v) for v in re.findall(r'^  Flags: (\d+)', text, re.M)]
    if len(flags) != 1 or not flags[0] & MASTER or flags[0] & DESTROY_ON_LEAVE:
        errors.append(f'{relative}: world enemy must follow master and survive owner leaving')
for name in ['A_FusionAstronaut', 'A_SurvivalAstronaut']:
    text = (ROOT / f'Assets/02_Prefabs/Fusion/{name}.prefab').read_text(encoding='utf-8')
    flags = [int(v) for v in re.findall(r'^  Flags: (\d+)', text, re.M)]
    if not flags or flags[0] & MASTER or not flags[0] & DESTROY_ON_LEAVE:
        errors.append(f'{name}: players must keep individual authority and despawn on departure')
settings = (ROOT / 'ProjectSettings/EditorBuildSettings.asset').read_text(encoding='utf-8')
for scene in ['j_LobyDeEspera', 'Mars', 'b_NeptuneExploration']:
    if f'path: Assets/00_Scenes/{scene}.unity' not in settings:
        errors.append(f'{scene}: missing from global build scene list')
crosshair = (ROOT / 'Assets/02_Prefabs/UI/j_PlayerCrosshair.prefab').read_text(encoding='utf-8')
if 'm_LocalScale: {x: 0, y: 0, z: 0}' in crosshair:
    errors.append('Crosshair has a zero-scale transform')
if errors:
    print('\n'.join(errors)); sys.exit(1)
print('PASS: five enemy authority policies, two player departure policies, campaign scene references and crosshair scale')
