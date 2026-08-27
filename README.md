# AirPlay 2 TV TCL Android 11 R51MT05

<p align="center">
  <img src="AirPlay.Android/Resources/drawable-nodpi/app_icon.png" alt="Icône AirPlay 2 TV TCL : petit robot vert croquant une pomme rouge sur fond noir" width="240">
</p>

Portage expérimental du moteur open source [SteeBono/airplayreceiver](https://github.com/SteeBono/airplayreceiver) pour les téléviseurs TCL G03 sous Android TV.

L’objectif est de conserver l’interface TCL et d’utiliser le moteur SteeBono pour la réception AirPlay audio et la recopie d’écran, avec une intégration directe à `TVInputService`.

> [!WARNING]
> La version **v10.0 reste expérimentale**. L’application et le service démarrent sur le téléviseur de test, mais les problèmes d’affichage de la recopie vidéo restent à corriger et à valider en conditions réelles.

## État actuel

Fonctionnalités déjà intégrées :

- deux récepteurs mDNS séparés : **TCL G03 Audio** et **TCL G03 Video** par défaut ;
- interface sombre inspirée d’Apple TV avec symbole AirPlay et navigation adaptée à la télécommande ;
- écran audio optionnel avec pochette, titre, artiste, album, progression et commandes lecture/pause/précédent/suivant via DACP ;
- retour automatique au menu standard lorsque cet affichage est désactivé ou que la session se termine ;
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

### Capture sur la TV de test

![Menu TCL R51MT05 montrant l’application AirPlay 2 TV TCL en haut et l’entrée système AirPlay sélectionnée en bas](docs/images/tcl-r51mt05-airplay-two-entries.png)

Capture effectuée sur la TV `192.168.1.35` : l’application **AirPlay 2 TV TCL**
est visible dans la rangée supérieure et l’entrée système **AirPlay** est
sélectionnée dans le bandeau inférieur. Le pont v10 conserve cette tuile mais
redirige son ouverture vers notre application.

La documentation technique détaillée se trouve dans [docs/ANDROID_TCL_G03.md](docs/ANDROID_TCL_G03.md).

## Limitations connues

- l’image peut être noire, mal cadrée ou non adaptée à la dalle selon l’orientation et le format source ;
- certaines activités système TCL peuvent masquer ou remplacer la surface vidéo ;
- les changements portrait/paysage et les reconnexions doivent encore être fiabilisés ;
- les sessions longues et les changements de source demandent davantage de tests ;
- l’interface v10.0 est fonctionnelle et affichée sur la TV de test, mais son utilisation prolongée à la télécommande doit encore être validée ;
- le module cible précisément la configuration TCL Android TV 11 R51MT05 rootée, sous firmware V652 et en ARMv7, utilisée pour les essais ;
- ce projet n’est pas encore une solution AirPlay 2 certifiée ou universelle.

## Version v10.0 tout compris

L’archive prête à installer est :

`tcl-airplay-v10.0-all-in-one-magisk.zip`

Elle contient :

- le lanceur et l’intégration TV TCL ;
- le lecteur SteeBono adapté à Android ;
- l’application Android **10.0** (`versionCode 25`) avec la nouvelle interface audio optionnelle ;
- la redirection de l’entrée système **AirPlay** du menu TCL vers l’application,
  sans modifier le chemin `TVInputService` utilisé pendant la projection vidéo ;
- le maintien du paquet TCL `com.tcl.airplay2` et de son récepteur comme
  déclencheur de la tuile ; le pont root intercepte `Show.Home.AirplayAPK`,
  arrête le processus hérité avant l’erreur **904**, puis ouvre directement la
  v10, tout en gardant `BootupService` disponible pour la bascule vidéo ;
- le nom de récepteur personnalisable et ses suffixes fixes **Audio** et **Video** ;
- les bibliothèques natives ARMv7 AAC et ALAC ;
- le service de démarrage Magisk ;
- les scripts d’installation et de désinstallation ;
- les licences et la documentation embarquée.

SHA-256 de l’archive validée :

```text
a30ceca381390629a5aba8909c026dc0c712fae487587f5b1c41a6d4df906c91
```

Voir [docs/RELEASE_V10.0.md](docs/RELEASE_V10.0.md) pour les détails de cette version.

## Installation Magisk

1. Copier `tcl-airplay-v10.0-all-in-one-magisk.zip` sur le téléviseur.
2. Ouvrir Magisk, choisir **Modules**, puis **Installer depuis le stockage**.
3. Sélectionner l’archive et redémarrer le téléviseur.
4. Vérifier que **TCL G03 Audio** et **TCL G03 Video** apparaissent sur l’appareil Apple, ou les deux noms dérivés du nom personnalisé.

Identifiant du module :

```text
tcl_airplay_g08_g03_minsdk30
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
