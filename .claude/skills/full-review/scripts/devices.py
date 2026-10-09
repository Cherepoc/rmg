"""The musical devices, by family, for the device and genre-fit reviewers: every genre choice (`GenreChoices`) and every
stage (`SongStages`), each in the family it belongs to. Writes <out>/devices.json and <out>/devices/<family>.txt, and
prints the families and every device it could not place, which is to be placed by hand before the reviewers start.

    python3 devices.py <repo> <out>
"""
import json, os, re, sys

root, out = sys.argv[1], sys.argv[2]
choices_cs = open(os.path.join(root, 'Rmg.Core/Composition/GenreChoices.cs')).read()
stages_cs = open(os.path.join(root, 'Rmg.Core/Composition/SongStages.cs')).read()

choices = re.findall(r'public static GenreChoice \w+ \{ get; \} = [^"]*?\("(\w+)", "([^"]+)"', choices_cs)
stages = re.findall(r'new\("([^"]+)", [\w.]+, CanSkip', stages_cs)

FAMILIES = {
    'form': {
        'choices': 'tempo length meter formChances form phrases phraseBars sectionLength phraseScheme texture alone loop drop dropKind breakdown stopTime '
                   'push intro riffIntro ending lastRepeat sectionChanges parts dynamics voice space rests',
        'stages': 'Stop time|Pushes|Silent start|Lift cues|Risers|Key changes',
    },
    'harmony': {
        'choices': 'rootMotion rootDegree cadenceDegree cadenceRaise sectionHome chordScales ownMode passingChords passingKind planing harmonyDevices '
                   'turnaround vamp keyChanges harmonicRhythm scale chordShape colour arpeggio gateParts harmonyInterval',
        'stages': 'Reharmonisation|Inner lines|Pedal|Line chords',
    },
    'lines': {
        'choices': 'bass octaveBass alternatingBass bassLanding glide melodyRange lineScale riffScale riff register doubling ornaments tension '
                   'inversion hook',
        'stages': 'Lines|Doubling|Chugs|Ornaments|Notes',
    },
    'drums': {
        'choices': 'fills pocket grooveHold pins mutation ghosts rudiments hatRolls drumSounds drumStrokes drumJob marks kit drumSetup drumPieces '
                   'polymeter feel feelChances feelValues',
        'stages': 'Polymeter|Figures|Mutations|Fills|Drum marks|Hat rolls|Ghosts|Drum ornaments|Timing',
    },
    'solos': {
        'choices': 'solos soloists drumSoloFeet sticking soloAnswers',
        'stages': 'Solos|Drum solos|Solo answers',
    },
    'sound': {
        'choices': 'effects strongEffect pinchSound louderForms instrumentSwitches room insertion pan mixFilter levels articulation instrument',
        'stages': 'Louder forms|Instruments|Articulation|Mix filter|Expression',
    },
}

placed = {f: {'choices': [], 'stages': []} for f in FAMILIES}
unplaced = []
for name, label in choices:
    family = next((f for f, d in FAMILIES.items() if name in d['choices'].split() or name.startswith('figure') and f == 'drums'), None)
    (placed[family]['choices'].append(f'{name} ({label})') if family else unplaced.append(f'choice {name} ({label})'))
for stage in stages:
    family = next((f for f, d in FAMILIES.items() if stage in d['stages'].split('|')), None)
    (placed[family]['stages'].append(stage) if family else unplaced.append(f'stage {stage}'))

os.makedirs(os.path.join(out, 'devices'), exist_ok=True)
json.dump({'families': placed, 'unplaced': unplaced}, open(os.path.join(out, 'devices.json'), 'w'), indent=1)
for family, d in placed.items():
    with open(os.path.join(out, 'devices', family + '.txt'), 'w') as f:
        f.write('Genre choices (GenreChoices.cs):\n' + '\n'.join(f'  {x}' for x in d['choices']) +
                '\nStages (SongStages.cs):\n' + '\n'.join(f'  {x}' for x in d['stages']) + '\n')
    print(f"{family:8} {len(d['choices']):3} choices, {len(d['stages']):2} stages")
print(f"{len(choices)} choices and {len(stages)} stages; unplaced: {', '.join(unplaced) or 'none'}")
