-> start

=== start ===
bonjour.
bienvenue dans ce test de dialogue pour sarabande.

+ [Continuer]
    -> intro_suite

=== intro_suite ===
tu avances dans le couloir.
l'air semble un peu plus lourd ici.

les murs portent encore la mémoire des voix passées.
chaque pas donne l'impression d'entrer un peu plus profondément dans la scène.

tu hésites une seconde.
puis tu décides d'aller jusqu'au bout.

+ [ouvrir la porte de gauche]
    -> gauche

+ [ouvrir la porte de droite]
    -> droite

=== gauche ===
tu ouvres la porte de gauche.
une lumière douce traverse la pièce, comme si quelqu'un venait juste de partir.
-> retour_commun

=== droite ===
tu ouvres la porte de droite.
la pièce est plus froide, plus nue, presque hostile.
-> retour_commun

=== retour_commun ===
quoi qu'il en soit, te voilà désormais de l'autre côté.

tu comprends que ce lieu te demande une réponse.
pas une réponse parfaite.
juste une réponse sincère.

+ [chanter]
    -> chanter_fin

+ [te taire]
    -> silence_fin

+ [partir]
    -> partir_fin

=== chanter_fin ===
tu commences à chanter.
le lieu semble vibrer avec toi, comme s'il attendait précisément cela.
-> END

=== silence_fin ===
tu choisis le silence.
et pour une fois, il ne ressemble pas à une fuite, mais à une forme de vérité.
-> END

=== partir_fin ===
tu fais demi-tour.
le lieu ne te retient pas, mais quelque chose en toi sait que tu reviendras.
-> END