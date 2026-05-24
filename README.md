# Rapport de projet

Issa MAHAMAT ISSA
Florian BOZEL

## Présentation

Notre projet est un simulateur de handicaps visuels en réalité mixte sur Meta Quest 3S. L'objectif est la sensibilisation : faire vivre au joueur, dans son propre environnement, ce que ressent une personne atteinte de glaucome, photophobie ou nystagmus. Le casque a été choisi pour la qualité de son passthrough, qui permet d'appliquer les effets de pathologie directement par-dessus la vue réelle plutôt que sur un décor purement virtuel.

L'audience est large : tout le monde, sans connaissance médicale ni habitude des manettes. C'est ce qui a guidé toute la conception de l'interface, inspirée du menu Nintendo Wii, gros boutons, navigation simple, peu d'animation, faible charge cognitive, pour rester accessible aux enfants, aux personnes âgées et aux publics peu à l'aise avec la technologie.


## Architecture et parcours utilisateur

Le parcours suit fidèlement la storyline du cahier des charges : Bienvenue → Sélection de la pathologie → Détail → Simulation, interaction, compréhension → Challenge → retour menu. Chaque pathologie est une scène Unity indépendante, héritant d'une classe commune `HandicapSimulationBase` qui orchestre quatre phases : Explanation, Simulation, Challenge, Correction. Cette séparation rend l'ajout d'un nouveau handicap très rapide.

Le menu principal (`MRMenuController`) construit toute son UI procéduralement, pour rester maintenable et éviter la dépendance à des prefabs bancales. On y retrouve un carrousel d'intro (5 slides : Bienvenue, Comment ça marche, Sécurité, Santé, Prêt ?), une grille de 8 tuiles, et un écran de détail par pathologie.


## Must-Haves - ce qui a été tenu

### Immersivité

La simulation place réellement le joueur dans la peau du patient. Le glaucome n'est pas une vignette douce : le centre devient à peine distinguable, la vue latérale devient totalement opaque, et l'effet suit l'orientation du casque. La photophobie ne se contente pas d'éclaircir un overlay virtuel : elle modifie réellement la luminosité du passthrough Meta, donc la vraie pièce devient éblouissante. Le nystagmus reproduit les vraies formes d'onde cliniques (jerk avec phase lente + saccade rapide, pendulaire sinusoïdal).

### Interactivité

Tout passe par la manette ou par le mouvement physique. Le joueur se déplace réellement dans la pièce, pas de téléportation joystick, et le tracking est ancré au sol, donc les guides de la scène glaucome restent au sol quand le joueur avance. Une canne d'aveugle virtuelle est attachée à la manette droite et suit son axe en temps réel ; elle vibre au contact des dalles de guidage avec un haptique calibré sourd et bref pour rester réaliste. Pour la photophobie, le joueur lève physiquement sa manette gauche entre sa tête et la lampe virtuelle pour faire écran, et doit tenir l'alignement deux secondes, l'anneau de progression vert est attaché à la main pour rester toujours visible.


### Effets spéciaux

L'effet ne sert pas le réalisme pour le réalisme, il modifie la perception du réel. Pour le glaucome, fondu sigmoïdal radial + bruit procédural pour casser l'effet vignette trop simple. Pour la photophobie, post-processing AR (brightness +0.45, contraste +0.20, saturation -0.25) plus un voile blanc-chaud, plus un cône de glare déclenché quand le joueur regarde directement une source. Pour le nystagmus, l'oscillation est appliquée à la rotation du tracking space, donc tout le monde virtuel et la vue AR oscillent ensemble, contrairement à un simple shake de caméra qui aurait été ignoré par le passthrough.

L'œil 3D affiché pendant la phase d'explication met en valeur la partie défaillante : nerf optique pour le glaucome, pupille rouge avec animation de dilatation cyclique pour la photophobie, muscles oculomoteurs pour le nystagmus.

## Game Mechanics - réalisé vs prévu

Le cahier des charges listait cinq mécaniques cibles. Trois ont été réalisées intégralement, deux n'ont pas pu l'être.

**Utiliser une canne virtuelle** - fait. Implémentée dans la scène glaucome, attachée à l'axe de la manette droite, avec détection de collision  sur les bandes des dalles et retour haptique.

**Tous les handicaps à tester** - partiellement fait. Trois pathologies sont entièrement jouables (glaucome, photophobie, nystagmus) sur les huit prévues dans le menu. Les cinq autres (daltonisme, myopie, dyslexie, rétinopathie diabétique, troubles visiospatiaux) apparaissent dans la grille mais ne lancent pas encore de scène implémentée.

**Attraper et lancer des objets** - partiellement fait. Le grab est présent pour saisir l'oeil et l'inspecter de près mais n'a été implémenté à aucun gameplay. C'était envisagé pour les troubles visiospatiaux (attraper un objet mal localisé en profondeur) mais cette scène n'a pas été créée.

**OCR sur une feuille pour la dyslexie** - non fait. La scène dyslexie n'a pas été commencée. L'idée d'utiliser une caméra passthrough pour lire un vrai papier puis remixer les lettres aurait demandé de l'OCR que le temps n'a pas permis d'intégrer, et qui pose en plus des questions de latence sur le casque.

**Verser de l'eau** - non fait. Cette mécanique servait à illustrer la perte de coordination ou la mauvaise perception de la profondeur. Elle aurait demandé une gestion de fluide qui n'a pas été abordée.

## Gameplay - limites rencontrées

Le challenge du glaucome (suivre un chemin de dalles guidé à la canne) est fonctionnel mais reste linéaire : un seul tracé prédéfini, pas de génération procédurale ni de difficulté progressive.

Le challenge de la photophobie a été retravaillé plusieurs fois. La première version "soleil au plafond" a été abandonnée parce que pas assez interactive avec le réel. La version finale (trois lampes: lampadaire, lustre, lampe de chevet, à éteindre en faisant écran avec la main) fonctionne mais reste court. Pas de système de score, pas de progression de difficulté.

Le panneau Statistiques prévu en fin de parcours dans le user journey n'a pas été implémenté. Aujourd'hui, après la phase Correction, le joueur revient simplement au menu, il n'y a pas d'écran qui récapitule le temps passé, le score ou un comparatif avec/sans correction.

La phase Correction existe pour chaque pathologie implémentée et permet de voir l'effet "lunettes correctrices", mais elle reste contemplative : le joueur ne refait pas le challenge avec la correction active, ce qui aurait été le moyen le plus pédagogique de démontrer l'apport du traitement.


## Conclusion

Le travail restant est moins technique que créatif : concevoir un challenge interactif et représentatif pour chacune des cinq pathologies non implémentées, et fermer la boucle pédagogique avec un écran de statistiques et une comparaison "avant/après correction" jouée plutôt que regardée. Pour le côté technique, il reste à implémenter l'OCR et la gestion de l'eau.
