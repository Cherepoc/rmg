
module RMG.Core.Tuples

    let structFst<'T1, 'T2> ((value, _): struct ('T1 * 'T2)) : 'T1 = value

    let structSnd<'T1, 'T2> ((_, value): struct ('T1 * 'T2)) : 'T2 = value
