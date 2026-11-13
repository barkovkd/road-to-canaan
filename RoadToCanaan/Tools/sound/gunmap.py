"""Which recordings of The Free Firearm Sound Library ('Prepared' set, CC0) speak for which of the game's guns.
Each gun of the library was recorded twice: near the shooter (left-right pair) and further off (forward-back);
'<id>' are the near takes, '<id>_far' the far ones (the game picks them for shots heard from a distance).
(id, files, gain dB: how far the body is raised under the limiter - the .22s least)."""
SHOTS = [
    # the revolver: a .45 (the 1911's report is the heavy one) and a .38 revolver
    ("shot_revolver", ["A_42P.wav", "V_27P.wav"], 8.0),
    ("shot_revolver_far", ["A_34P.wav", "V_22P.wav"], 5.0),
    # the repeater: two lever actions, the Winchester 1894 and the Marlin 336
    ("shot_repeater", ["L_23P.wav", "I_22P.wav"], 8.0),
    ("shot_repeater_far", ["L_17P.wav", "I_17P.wav"], 5.0),
    # the rifle: heavy bolt actions (Springfield 1917, Tikka .30-06, Mosin Nagant)
    ("shot_rifle", ["B_24P.wav", "W_29P.wav", "M_21P.wav"], 8.0),
    ("shot_rifle_far", ["B_16P.wav", "W_24P.wav", "M_26P.wav"], 5.0),
    # the shotgun: 12 gauges (Winchester Model 12, Mossberg 190, Benelli Nova)
    ("shot_shotgun", ["K_22P.wav", "N_30P.wav", "O_21P.wav"], 8.0),
    ("shot_shotgun_far", ["K_17P.wav", "N_26P.wav", "O_17P.wav"], 5.0),
    # the varmint gun: .22s (Ruger Single Six, Ruger Mark III)
    ("shot_small", ["S_14P.wav", "R_35P.wav"], 3.0),
    ("shot_small_far", ["S_11P.wav", "R_30P.wav"], 1.0),
]
