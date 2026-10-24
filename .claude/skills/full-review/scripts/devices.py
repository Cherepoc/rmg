"""The musical devices, by family, for the device reviewers: every genre choice (`GenreChoices`) and every stage
(`SongStages`), each in the family it belongs to; every genre preset, by its cluster of styles, for the genre-fit
reviewers; and the stages in their order, for the primer. Writes <out>/devices.json, <out>/devices/<family>.txt,
<out>/genres.json, <out>/genres/<cluster>.txt and <out>/stages.txt, and prints the families, every device and preset it
could not place and every name in its tables the code no longer has, each to be put right in the tables below before the
reviewers start.

    python3 devices.py <repo> <out>
"""
import json, os, re, sys

root, out = sys.argv[1], sys.argv[2]
choices_cs = open(os.path.join(root, 'Rmg.Core/Composition/GenreChoices.cs')).read()
stages_cs = open(os.path.join(root, 'Rmg.Core/Composition/SongStages.cs')).read()

choices = re.findall(r'public static GenreChoice \w+ \{ get; \} = [^"]*?\("(\w+)", "([^"]+)"', choices_cs)
declared = re.findall(r'new\("([^"]+)", [\w.]+, CanSkip: (\w+)', stages_cs)
stages = [name for name, _ in declared]

FAMILIES = {
    'form': {
        'choices': 'tempo length meter formChances form phrases phraseBars sectionLength phraseScheme texture alone loop drop dropKind breakdown stopTime '
                   'push intro introWindow openingAlone openingPart ensembleHits ending lastRepeat sectionChanges parts roleLength dynamics voice space rests '
                   'tempoBand intensity stops surfDrop riffSection',
        'stages': 'Stop time|Pushes|Silent start|Lift cues|Risers|Surf drops|Key changes|Hook intro',
    },
    'harmony': {
        'choices': 'rootMotion rootDegree cadenceDegree cadenceRaise sectionHome chordScales ownMode passingChords passingKind planing harmonyDevices '
                   'turnaround vamp keyChanges keyLift sectionKey sectionPedal harmonicRhythm scale chordShape colour arpeggio arpeggio.chords arpeggio.rhythm gateParts '
                   'harmonyInterval strictness darkness',
        'stages': 'Reharmonisation|Inner lines|Pedal|Line chords',
    },
    'lines': {
        'choices': 'bass octaveBass alternatingBass bassLanding glide melodyRange lineScale riffScale riff register doubling ornaments tension '
                   'hook appoggiatura callAndResponse sung glideRate lineRun sequence lineChords',
        'stages': 'Lines|Doubling|Chugs|Ornaments|Notes|Call and response|Kick-out bass',
    },
    'drums': {
        'choices': 'fills fillFeet pocket grooveHold pins mutation ghosts rudiments hatRolls drumSounds drumStrokes drumJob marks kit drumSetup drumPieces '
                   'polymeter polymeterCell feel feelChances feelValues ghostNotes hatRollRate kickOut timeChanges',
        'stages': 'Polymeter|Figures|Mutations|Fills|Drum marks|Hat rolls|Ghosts|Drum ornaments|Timing',
    },
    'solos': {
        'choices': 'solos soloists soloCount soloShare drumSoloFeet sticking soloAnswers soloStyle soloLanding',
        'stages': 'Solos|Drum solos|Solo answers',
    },
    'sound': {
        'choices': 'effects strongEffect echo louderForms instrumentSwitches room insertion pan mixFilter levels articulation articulationModes bendTiming vibratoWidth',
        'stages': 'Instruments|Articulation|Mix filter|Expression',
    },
}

placed = {f: {'choices': [], 'stages': []} for f in FAMILIES}
unplaced = []
for name, label in choices:
    family = next((f for f, d in FAMILIES.items() if name in d['choices'].split() or name.startswith('figure') and f == 'drums'), None)
    (placed[family]['choices'].append(f'{name} ({label})') if family else unplaced.append(f'choice {name} ({label})'))
# the parts' instrument pools are a dictionary by role (GenreChoices.Instruments), which the pattern above does not see
placed['sound']['choices'].append("instrument (every pitched part's instrument pool, GenreChoices.Instruments)")
for stage in stages:
    family = next((f for f, d in FAMILIES.items() if stage in d['stages'].split('|')), None)
    (placed[family]['stages'].append(stage) if family else unplaced.append(f'stage {stage}'))

# a name in the tables the code no longer has: a device renamed or gone, to take out of the table
choice_names = {name for name, _ in choices}
stale = [f'choice {c}' for d in FAMILIES.values() for c in d['choices'].split() if c not in choice_names]
stale += [f'stage {x}' for d in FAMILIES.values() for x in d['stages'].split('|') if x not in stages]

# the genre presets by cluster of styles, a genre-fit reviewer each: the neighbours a preset is told apart from
CLUSTERS = {
    'rock-pop': 'rock pop ballad punk surf',
    'heavy': 'metal nuMetal djent blackMetal thrash doom symphonic',
    'blues-jazz': 'blues jazz fusion soul darkJazz jazzFunk',
    'groove': 'funk funkRock disco latin bossa reggae dub ska',
    'club': 'electronic techno trance psytrance dnb idm',
    'beats': 'hipHop trap synthwave chiptune fmGame',
    'folk': 'folk country celtic bluegrass medieval lyrical',
    'concert': 'orchestral chamber filmPiano dungeon ambient',
}
presets = re.findall(r'^    "id": "(\w+)"', open(os.path.join(root, 'Rmg.WebApi/Client/genres.json')).read(), re.M)
clusters = {c: [p for p in names.split() if p in presets] for c, names in CLUSTERS.items()}
unplaced += [f'preset {p}' for p in presets if not any(p in v for v in clusters.values())]
stale += [f'preset {p}' for names in CLUSTERS.values() for p in names.split() if p not in presets]
os.makedirs(os.path.join(out, 'genres'), exist_ok=True)
for c, names in clusters.items():
    with open(os.path.join(out, 'genres', c + '.txt'), 'w') as f:
        f.write('\n'.join(names) + '\n')
json.dump(clusters, open(os.path.join(out, 'genres.json'), 'w'), indent=1)

# the stages in their order, for the primer: those before Notes edit the state, those after it the notes
with open(os.path.join(out, 'stages.txt'), 'w') as f:
    f.write('\n'.join(f"{k:2} {name}{'' if skip == 'true' else ' (always runs)'}" for k, (name, skip) in enumerate(declared, 1)) + '\n')

os.makedirs(os.path.join(out, 'devices'), exist_ok=True)
json.dump({'families': placed, 'unplaced': unplaced, 'stale': stale}, open(os.path.join(out, 'devices.json'), 'w'), indent=1)
for family, d in placed.items():
    with open(os.path.join(out, 'devices', family + '.txt'), 'w') as f:
        f.write('Genre choices (GenreChoices.cs):\n' + '\n'.join(f'  {x}' for x in d['choices']) +
                '\nStages (SongStages.cs):\n' + '\n'.join(f'  {x}' for x in d['stages']) + '\n')
    print(f"{family:8} {len(d['choices']):3} choices, {len(d['stages']):2} stages")
print(f"{len(choices)} choices and {len(stages)} stages, written in order to stages.txt")
for c, names in clusters.items():
    print(f"genre cluster {c:10} {' '.join(names)}")
print(f"unplaced: {', '.join(unplaced) or 'none'}")
print(f"stale: {', '.join(stale) or 'none'}")
if unplaced or stale:
    sys.exit(1)
