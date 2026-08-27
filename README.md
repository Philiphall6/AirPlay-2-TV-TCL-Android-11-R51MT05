# AirPlay 2 TV TCL Android 11 R51MT05

<p align="center">
  <img src="AirPlay.Android/Resources/drawable-nodpi/app_icon.png" alt="Icône AirPlay 2 TV TCL : petit robot vert croquant une pomme rouge sur fond noir" width="240">
</p>

Portage expérimental du moteur open source [SteeBono/airplayreceiver](https://github.com/SteeBono/airplayreceiver) pour les téléviseurs TCL G03 sous Android TV.

L’objectif est de conserver l’interface TCL et d’utiliser le moteur SteeBono pour la réception AirPlay audio et la recopie d’écran, avec une intégration directe à `TVInputService`.

> [!WARNING]
> La version **v9.23 reste expérimentale**. L’audio fonctionne lors des essais actuels, mais des problèmes d’affichage restent à corriger. L’interface TCL/SteeBono doit également être terminée.

## État actuel

Fonctionnalités déjà intégrées :

- deux récepteurs mDNS séparés : **TCL G03 Audio** et **TCL G03 Video** par défaut ;
- interface sombre inspirée d’Apple TV avec symbole AirPlay et navigation adaptée à la télécommande ;
- nom de base personnalisable, mémorisé localement, avec suffixes fixes **Audio** et **Video** ;
- réception audio PCM, AAC, AAC-ELD et ALAC ;
- bibliothèques natives ARMv7 `libfdk-aac.so` et `libalac.so` ;
- sortie audio Android par `AudioTrack` ;
- décodage vidéo H.264 avec le codec matériel Realtek via `MediaCodec` ;
- gestion du flux TCP, des paquets multi-NAL, des IDR et des paramètres SPS/PPS ;
- tentative de récupération automatique du décodeur vidéo ;
- rendu vers la `Surface` fournie par `TVInputService` ;
- conservation du ratio et centrage de l’image en portrait ou paysage ;
- démarrage du service mDNS au démarrage du téléviseur ;
- bascule vers l’entrée AirPlay lors du début d’une session ;
- aucun enregistrement permanent des contenus audio ou vidéo reçus.

### Configuration testée

- téléviseur TCL sous **Android TV 11** ;
- plateforme **R51MT05** ;
- firmware **V652** ;
- téléviseur **rooté avec Magisk** ;
- architecture **ARMv7 (`armeabi-v7a`)**.

Cette mention décrit la configuration physique utilisée pendant les essais ; elle ne garantit pas encore le fonctionnement sur les autres firmwares ou plateformes TCL.

La documentation technique détaillée se trouve dans [docs/ANDROID_TCL_G03.md](docs/ANDROID_TCL_G03.md).

## Limitations connues

- l’image peut être noire, mal cadrée ou non adaptée à la dalle selon l’orientation et le format source ;
- certaines activités système TCL peuvent masquer ou remplacer la surface vidéo ;
- les changements portrait/paysage et les reconnexions doivent encore être fiabilisés ;
- les sessions longues et les changements de source demandent davantage de tests ;
- l’interface utilisateur n’est pas terminée ;
- le module cible précisément la configuration TCL Android TV 11 R51MT05 rootée, sous firmware V652 et en ARMv7, utilisée pour les essais ;
- ce projet n’est pas encore une solution AirPlay 2 certifiée ou universelle.

## Version v9.23 tout compris

L’archive prête à installer est :

`tcl-airplay-v9.23-all-in-one-magisk.zip`

Elle contient :

- le lanceur et l’intégration TV TCL ;
- le lecteur SteeBono adapté à Android ;
- l’application Android AirPlay Receiver 0.3.18 (`versionCode 21`) ;
- les bibliothèques natives ARMv7 AAC et ALAC ;
- le service de démarrage Magisk ;
- les scripts d’installation et de désinstallation ;
- les licences et la documentation embarquée.

SHA-256 de l’archive validée :

```text
71eedc4958103f25c4efc4ee41844c5090229b74196316b04ef4e5d9be331a5b
```

Voir [docs/RELEASE_V9.23.md](docs/RELEASE_V9.23.md) pour les détails de cette version.

## Installation Magisk

1. Copier `tcl-airplay-v9.23-all-in-one-magisk.zip` sur le téléviseur.
2. Ouvrir Magisk, choisir **Modules**, puis **Installer depuis le stockage**.
3. Sélectionner l’archive et redémarrer le téléviseur.
4. Vérifier que **TCL G03 Audio** et **TCL G03 Vidéo** apparaissent sur l’appareil Apple.

Identifiant du module :

```text
tcl-airplay-hybrid-v8
```

En cas de problème au démarrage, supprimer le module depuis l’environnement de récupération ou avec ADB, puis redémarrer.

## Construction Android

Prérequis principaux : Android SDK, Android NDK et Java compatibles avec le projet.

Construction de l’application TCL G03 :

```bash
./scripts/build-android-g03.sh
```

Construction des codecs natifs ARMv7 :

```bash
./scripts/build-native-codecs-armv7.sh
```

Génération de l’archive Magisk hybride :

```bash
python3 scripts/build-hybrid-v8-magisk.py
```

Le script `scripts/patch-tcl-g03-player.py` applique les adaptations du lecteur TCL lorsque la base système correspond à la version attendue.

## Architecture

```text
iPhone / iPad / Mac
        │
        ├── AirPlay audio ──► moteur SteeBono ──► AudioTrack
        │
        └── recopie H.264 ──► moteur SteeBono ──► MediaCodec Realtek
                                                        │
                                                        ▼
                                             Surface TVInputService TCL
```

## Confidentialité

Le récepteur traite les flux en mémoire pendant la session. L’adaptation n’a pas vocation à sauvegarder durablement la musique, la vidéo ou la recopie d’écran. Seuls les réglages de l’application, notamment le nom choisi pour le récepteur, sont conservés localement. Les clés privées de signature et les informations propres au téléviseur ne doivent pas être ajoutées au dépôt ni aux archives de publication.

## Crédits et licence

Le moteur AirPlay d’origine est développé par [SteeBono](https://github.com/SteeBono/airplayreceiver). Les modifications de ce port sont distribuées avec les licences présentes dans le dépôt et dans l’archive de publication.

AirPlay est une marque d’Apple Inc. TCL, Android et les autres marques citées appartiennent à leurs détenteurs respectifs. Ce projet communautaire n’est affilié ni à Apple ni à TCL.
