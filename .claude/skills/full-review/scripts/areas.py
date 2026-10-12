"""The area a finding's file belongs to, which the verifiers' batches and the report's sections follow."""
import os


def area(path, category=None):
    b = os.path.basename(path)
    if category in ('run', 'flaky'):
        return 'run'
    if category == 'genre-fit':
        return 'genre-fit'
    if path.startswith('Rmg.Tests.Page'):
        return 'page-tests'
    if path.startswith('Rmg.Tests'):
        return 'tests'
    if path.endswith('genres.json'):
        return 'genre-presets'
    if path.startswith('Rmg.WebApi/Client'):
        return 'page'
    if path.startswith('Rmg.WebApi') or path.startswith('Rmg/'):
        return 'server'
    if path.startswith('Rmg.Core/Composition'):
        if b.startswith(('Drum', 'Fill', 'HatRolls', 'Ghosts', 'LiftCues', 'Figures', 'Mutations', 'Pocket', 'Groove', 'Feels', 'Polymeter', 'PatternGenerator', 'Percussion', 'SongDrumRule', 'SectionDrum')):
            return 'core-drums'
        if b.startswith(('Solo', 'DrumSolos')):
            return 'core-solos'
        if b.startswith(('Expression', 'Articulations', 'LouderForms', 'Instrument', 'DoubleTracking', 'Insertions', 'Panning')):
            return 'core-expression'
        if b.startswith('Genre'):
            return 'core-genres'
        if b.startswith(('Line', 'Melody', 'Counter', 'Riff', 'Bass', 'Phrase', 'Register', 'Ornament', 'Chugs', 'Breakdowns')):
            return 'core-lines'
        if b.startswith(('Progressions', 'Chord', 'Passing', 'Reharm', 'Planing', 'KeyChange', 'InnerLines', 'HarmonicRhythm', 'Scales', 'Voice', 'Arpeggios', 'Gated', 'Realizer')):
            return 'core-harmony'
        return 'core-pipeline'
    if path.startswith('Rmg.Core/Rendering') or b == 'Midi.cs':
        return 'core-render'
    return 'core-foundation'


TITLES = {
    'run': 'Test runs on other corpora',
    'genre-fit': 'Genre fit: devices missing, inherited or mis-set',
    'core-pipeline': 'Core: pipeline, form, sections, stages',
    'core-harmony': 'Core: harmony and realizing',
    'core-lines': 'Core: lines, melody, riff',
    'core-drums': 'Core: drums, fills, figures, groove',
    'core-solos': 'Core: solos',
    'core-expression': 'Core: expression and instruments',
    'core-genres': 'Core: genres',
    'core-render': 'Core: rendering and MIDI',
    'core-foundation': 'Core: foundation (events, probabilities, songs)',
    'genre-presets': 'Genre presets (genres.json)',
    'server': 'Server and CLI',
    'page': 'Page (client)',
    'tests': 'Tests',
    'page-tests': 'Page tests',
}
