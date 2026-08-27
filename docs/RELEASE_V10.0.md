# AirPlay 2 TV TCL Android 11 R51MT05 — v10.0

Version expérimentale complète du port Android TV ARMv7 fondé sur le moteur
SteeBono et l’intégration TCL `TVInputService`.

> **État expérimental :** l’application, l’interface et les services audio/vidéo
> démarrent sur la TV de test. Les problèmes historiques de cadrage et de rendu
> de la recopie vidéo doivent encore faire l’objet d’une validation complète.

## Nouveautés v10.0

- application Android `10.0` (`versionCode=22`) ;
- interface sombre inspirée d’Apple TV, sans barre de titre Android ;
- symbole AirPlay blanc et icône du petit robot croquant une pomme ;
- commandes et états de focus adaptés à la télécommande ;
- nom de base du récepteur modifiable et conservé localement ;
- suffixes non modifiables `Audio` et `Video` ;
- aperçu immédiat des deux noms annoncés ;
- redémarrage du service et des annonces mDNS après enregistrement du nom ;
- masquage de l’interface au démarrage du décodage H.264 afin de libérer
  l’affichage vidéo plein écran ;
- réaffichage de l’interface avec une touche de la télécommande.

## Fonctions conservées

- destinations AirPlay audio et vidéo séparées ;
- PCM, AAC/AAC-ELD et ALAC avec bibliothèques ARMv7 ;
- sortie Android `AudioTrack` ;
- H.264 par `MediaCodec` matériel Realtek ;
- pont direct vers la `Surface` du `TVInputService` TCL ;
- récupération IDR/SPS/PPS et traitement multi-NAL ;
- démarrage automatique par Magisk ;
- aucun enregistrement permanent des médias reçus.

## Configuration vérifiée

- Android TV 11 ;
- plateforme R51MT05 ;
- firmware V652 rooté avec Magisk 29 ;
- architecture `armeabi-v7a` ;
- installation APK par mise à jour réussie avec la même signature que v9.23 ;
- service au premier plan actif ;
- ports TCP audio 5000 et vidéo 7000 accessibles ;
- module Magisk v10.0 (`versionCode=34`) installé pour le prochain démarrage.

## Fichiers

### Module Magisk tout compris

`tcl-airplay-v10.0-all-in-one-magisk.zip`

SHA-256 :

`c772858f7a01f02bccd0c9eaf09d652a19afd44bfeedd71bcd5e8356ae013d07`

### APK Android ARMv7

`AirPlay-2-TV-TCL-R51MT05-v10.0-armv7.apk`

SHA-256 :

`5c7d3c6f98e66f6942fb6e71cad116e9276787f4aeb3b9efe24a89300fca5599`

## Installation

Installer le ZIP depuis Magisk puis redémarrer le téléviseur. Le module conserve
l’identifiant `tcl_airplay_g08_g03_minsdk30` et remplace donc la version
précédente. L’APK peut aussi être installé avec `adb install -r` pour une mise à
jour immédiate de l’application.

En cas de problème au démarrage, désactiver le module dans Magisk ou créer le
fichier `/data/adb/modules/tcl_airplay_g08_g03_minsdk30/disable` depuis un shell
root, puis redémarrer.
