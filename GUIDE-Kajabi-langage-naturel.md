# Piloter votre site Kajabi en langage naturel

**Guide pour débutant** — Kajabi peut désormais être branché à un assistant IA. Une fois
la connexion faite, vous écrivez ce que vous voulez changer, en français, en une phrase, et
l'assistant va le faire dans votre site. Voici comment mettre ça en place, sans une seule
ligne de code.

|  |  |
|---|---|
| **Durée** | ≈ 15 minutes |
| **Niveau** | Aucune compétence technique |
| **Coût** | Inclus dans votre offre Kajabi |
| **Réversible** | Oui, à tout moment |

*Rédigé le 18 août 2026 à partir de la documentation officielle Kajabi, Anthropic et OpenAI.*

---

## Sommaire

1. [L'idée en une image](#1--lidée-en-une-image)
2. [Ce que ça sait faire](#2--ce-que-ça-sait-faire--et-ce-quil-ne-faut-pas-espérer)
3. [Avant de commencer](#3--avant-de-commencer)
4. [Choisir votre outil](#4--choisir-votre-outil)
5. [Parcours A — Claude Cowork](#5--parcours-a--brancher-kajabi-à-claude-cowork)
6. [Parcours B — Codex](#6--parcours-b--brancher-kajabi-à-codex)
7. [L'écran des permissions](#7--lécran-des-permissions)
8. [Votre premier essai](#8--votre-premier-essai)
9. [Bien formuler vos demandes](#9--bien-formuler-vos-demandes)
10. [Les règles de prudence](#10--les-règles-de-prudence)
11. [Quand ça ne marche pas](#11--quand-ça-ne-marche-pas)
12. [Petit glossaire](#12--petit-glossaire)

---

## 1 · L'idée en une image

> **Imaginez que vous confiez un double de vos clés à un assistant très compétent, avec un mot
> qui précise exactement les pièces où il a le droit d'entrer.**

C'est très exactement ce que vous allez faire. Kajabi propose une « porte de service »
officielle pour les assistants IA. Vous décidez quelles pièces ouvrir (les articles ? les
pages ? les e-mails ?), l'assistant y travaille pour vous, et il vous rend son travail
**en brouillon** — c'est toujours vous qui appuyez sur « Publier » depuis Kajabi.

Le nom technique de cette porte de service est **MCP**. Vous n'avez pas besoin de comprendre
ce que ça veut dire, mais vous allez voir ces trois lettres partout, alors autant les
reconnaître. La seule chose à retenir : c'est une adresse à copier-coller, une fois, dans
votre assistant.

> **L'adresse en question**
> `https://mcp.kajabi.com/mcp`
> Gardez-la sous la main, vous en aurez besoin une seule fois, à l'étape 5 ou 6 selon
> l'outil que vous choisissez.

---

## 2 · Ce que ça sait faire — et ce qu'il ne faut pas espérer

Regardez cette liste avant d'aller plus loin : si ce que vous vouliez automatiser est dans la
colonne de droite, ce guide ne vous servira pas.

| ✅ L'assistant **peut** | ❌ L'assistant **ne peut pas** |
|---|---|
| Lire et modifier vos **pages** et **landing pages** | Vous sortir des **statistiques** ou analyses d'audience |
| Écrire et corriger vos **articles de blog** | Gérer votre **communauté** au-delà du strict minimum |
| Préparer vos **e-mails**, séquences et newsletters | Toucher aux **paiements**, remboursements ou facturation |
| Créer et ajuster vos **offres, produits et cours** | Agir **à la place d'un de vos clients** |
| Travailler sur vos **contacts, étiquettes et segments** | Publier tout seul ce que vous ne lui avez pas demandé |
| Consulter vos **commandes** |  |
| Retoucher les **réglages du site** et le contenu du thème |  |

Un exemple de ce que vous pourrez taper une fois branché, pour fixer les idées :

> *« Crée une offre à −30 % sur mon cours Photographie, valable mardi prochain, rédige
> l'e-mail d'annonce et mets à jour la landing page pour mentionner la remise. »*

Trois actions, une phrase.

---

## 3 · Avant de commencer

Quatre choses à vérifier. Comptez deux minutes.

### 1. Un compte Kajabi, avec les droits d'administration

Peu importe l'offre — Basic, Growth ou Pro, mensuelle ou annuelle : la connexion IA est
incluse partout, sans supplément. En revanche, il faut être administrateur du site, pas
simple collaborateur.

### 2. Être connecté au bon compte Kajabi dans votre navigateur

C'est la cause n°1 des échecs. Si vous gérez plusieurs comptes Kajabi, ouvrez votre
navigateur, connectez-vous au bon, et laissez cet onglet ouvert. L'écran d'autorisation
s'ouvrira dans ce navigateur-là.

### 3. Un assistant IA installé

Claude Cowork ou Codex — le choix se fait à l'étape suivante. Installez-le et connectez-vous
à votre compte avant de continuer.

### 4. Une idée précise de votre première demande

Pas un grand chantier. Quelque chose de minuscule et vérifiable : corriger une faute que vous
connaissez, changer une date dans un bandeau. Vous testez la tuyauterie, pas l'assistant.

> **Bon à savoir**
> Vous n'aurez **jamais** à donner votre mot de passe Kajabi à l'assistant. L'autorisation
> passe par une page officielle Kajabi, dans votre navigateur, où vous cochez vous-même les
> cases. Si un outil vous réclame votre mot de passe, arrêtez-vous.

---

## 4 · Choisir votre outil

Les deux fonctionnent avec Kajabi. Ils ne visent simplement pas le même public.

### Parcours A · Claude Cowork — *recommandé*

L'assistant « collègue de bureau » d'Anthropic. Tout se fait à la souris, dans des menus,
sans jamais ouvrir de terminal. C'est celui qui a été pensé pour les gens qui ne codent pas.

- **Pour qui** — vous gérez votre site seul et vous n'avez jamais écrit de code.
- **Mise en place** — 5 clics dans les réglages.
- **Abonnement** — un plan payant (Pro ou supérieur) est conseillé ; le plan gratuit
  n'autorise qu'une seule connexion de ce type.

### Parcours B · Codex

L'assistant d'OpenAI. Il existe en application et en ligne de commande. La version
application se configure aussi à la souris ; la version ligne de commande demande d'éditer un
petit fichier de réglages.

- **Pour qui** — vous l'utilisez déjà, ou quelqu'un vous a installé la version en ligne de commande.
- **Mise en place** — quelques clics, ou 3 lignes à coller dans un fichier.
- **Abonnement** — compte ChatGPT ; les droits d'écriture demandent selon les cas un plan professionnel.

> **Si vous hésitez**
> Prenez le parcours A. Vous pourrez toujours ajouter le second plus tard : brancher Kajabi à
> un deuxième assistant ne casse pas le premier, et les deux peuvent cohabiter sans problème.

---

## 5 · Parcours A — brancher Kajabi à Claude Cowork

Cinq étapes. Les libellés ci-dessous sont ceux de l'interface en anglais, qui reste la
version par défaut ; la traduction est indiquée entre parenthèses.

### Étape 1 — Ouvrez les réglages des connecteurs

Dans Cowork (ou Claude sur le web, ou l'application de bureau — c'est le même réglage) :

```
Customize  ›  Connectors
(Personnaliser › Connecteurs)
```

### Étape 2 — Ajoutez un connecteur personnalisé

Cliquez sur le bouton **+** à côté de « Connectors », puis sur **Add custom connector**
(Ajouter un connecteur personnalisé). Sur certaines versions, ce bouton se trouve tout en bas
de la page.

### Étape 3 — Remplissez les deux champs

Un nom, une adresse. Rien d'autre — laissez les « Advanced settings » (réglages avancés) tels
quels.

```
Name (nom)  →  Kajabi
URL         →  https://mcp.kajabi.com/mcp
```

Puis cliquez sur **Add** (Ajouter).

### Étape 4 — Autorisez, et cochez vos permissions

Une page Kajabi s'ouvre dans votre navigateur : c'est le moment important du guide. Ne validez
pas machinalement — [la section 7](#7--lécran-des-permissions) explique quoi cocher. Une fois
vos choix faits, approuvez.

### Étape 5 — Activez Kajabi dans la conversation

Le connecteur existe, mais il n'est pas encore allumé dans vos discussions. Ouvrez une nouvelle
conversation, cliquez sur le bouton **+** en bas à gauche, puis sur **Connectors**, et basculez
l'interrupteur Kajabi sur « activé ».

> **Détail qui compte**
> Cet interrupteur se règle par conversation. Si demain Kajabi semble avoir disparu, c'est
> presque toujours qu'il est simplement éteint dans la nouvelle discussion.

➡️ Passez directement à [la section 7](#7--lécran-des-permissions).

---

## 6 · Parcours B — brancher Kajabi à Codex

Deux chemins selon la version que vous utilisez. Prenez le premier si vous avez une fenêtre
avec des boutons, le second si vous avez une fenêtre noire où l'on tape du texte.

### B1 · Vous utilisez l'application Codex ou l'extension dans un éditeur

**Étape 1 — Ouvrez la gestion des serveurs MCP.** Dans les réglages de l'application, cherchez
la section MCP, puis **Add server** (Ajouter un serveur).

**Étape 2 — Choisissez le bon type.** Une liste vous demande le type de connexion. Choisissez
**Streamable HTTP** — c'est le type « serveur distant », par opposition aux programmes
installés sur votre machine.

**Étape 3 — Collez l'adresse et enregistrez.**

```
Nom  →  kajabi
URL  →  https://mcp.kajabi.com/mcp
```

L'écran d'autorisation Kajabi s'ouvre ensuite dans votre navigateur.
Voir [la section 7](#7--lécran-des-permissions) pour les permissions.

### B2 · Vous utilisez Codex en ligne de commande

Il faut ajouter trois lignes dans un fichier de réglages nommé `config.toml`. Il se trouve
dans un dossier caché `.codex`, à la racine de votre dossier personnel. S'il n'existe pas,
créez-le.

```toml
[mcp_servers.kajabi]
url = "https://mcp.kajabi.com/mcp"
auth = "oauth"
```

Enregistrez, puis lancez la connexion et vérifiez :

```bash
codex mcp login kajabi
codex mcp list
```

La première commande ouvre l'autorisation Kajabi dans votre navigateur ; la seconde doit faire
apparaître `kajabi` dans la liste. Enchaînez sur [la section 7](#7--lécran-des-permissions).

> ⚠️ **Attention aux guillemets**
> Si vous copiez ces lignes depuis un traitement de texte, les guillemets droits `"` peuvent
> être transformés en guillemets courbes « intelligents », et le fichier ne fonctionnera plus.
> Collez-les dans un éditeur de texte simple (Bloc-notes, TextEdit en mode texte brut).

---

## 7 · L'écran des permissions

C'est le seul écran de tout ce guide où une mauvaise case cochée peut vous coûter cher. Il
liste ce que l'assistant aura le droit de faire, chaque ligne s'activant indépendamment. Le
principe est simple : **n'accordez que ce dont vous avez besoin cette semaine.** Vous pourrez
toujours revenir en ajouter.

| Permission proposée | Concrètement | Notre conseil |
|---|---|---|
| **Lire vos données Kajabi** | L'assistant peut voir vos pages, contacts, commandes. Sans ça, il ne peut rien faire du tout. | ✅ **Cochez** |
| **Créer et modifier articles, landing pages, newsletters, formulaires et cours** | Le cœur du sujet : corriger un texte, réécrire une page, préparer un article. | ✅ **Cochez** |
| **Créer et modifier offres, produits, coaching et événements** | Monter une promotion, changer un prix, créer un nouveau produit. | 🟡 Selon besoin |
| **Créer et modifier étiquettes et segments de contacts** | Réorganiser votre base de contacts en groupes. | 🟡 Selon besoin |
| **Créer et modifier diffusions, séquences et newsletters** | Préparer vos campagnes e-mail. | 🟡 Selon besoin |
| **Envoyer des messages privés et publier dans la communauté** | Écrire à vos membres en votre nom. Ces publications-là partent en direct, pas en brouillon. | ❌ **Laissez décoché** |
| **Publier ou dépublier du contenu** | Rend le garde-fou du brouillon inopérant : l'assistant peut mettre en ligne lui-même. | ❌ **Laissez décoché** |
| **Modifier le contenu du thème, le JavaScript et le CSS** | Touche à l'apparence de tout le site. Une erreur ici est difficile à rattraper seul. | ❌ **Laissez décoché** |
| **Lister et ajouter des utilisateurs du compte** | Donner à quelqu'un d'autre l'accès à votre Kajabi. Désactivé par défaut, et c'est très bien ainsi. | ❌ **Laissez décoché** |

> ⚠️ **Les trois dernières lignes**
> « Publier », « thème et code », « utilisateurs » sont les trois permissions dont les effets
> sont les plus difficiles à annuler. Kajabi les signale d'ailleurs comme telles. Vivez un mois
> sans, puis rouvrez la question — vous saurez alors si vous en avez réellement besoin.

Une fois vos cases cochées, validez. C'est fini : la connexion est établie et l'assistant sait
parler à votre site.

---

## 8 · Votre premier essai

Toujours la même première phrase, quel que soit l'outil. Elle sert à vérifier que le tuyau est
ouvert — et l'assistant en a besoin pour savoir de quel site vous parlez.

**Étape 1 · vérification**

> Liste mes sites Kajabi.

Si l'assistant vous répond avec le nom de votre site, tout est en place. Enchaînez alors sur
une lecture, sans rien modifier :

**Étape 2 · lecture seule**

> Montre-moi le texte du bandeau en haut de ma page d'accueil.

**Étape 3 · première modification**

> Dans le pied de page, remplace « Tous droits réservés 2025 » par « Tous droits réservés
> 2026 ». Montre-moi d'abord ce que tu vas changer, avant de le faire.

Puis, sans exception : allez dans votre interface Kajabi et regardez le brouillon de vos
propres yeux. C'est le réflexe à installer dès le premier jour.

> **Pourquoi « liste mes sites » en premier**
> Presque tous les outils Kajabi ont besoin de savoir sur quel site travailler. Tant que
> l'assistant n'a pas posé cette question, il risque de tourner en rond ou de vous demander un
> identifiant que vous n'avez pas. Cette phrase lui donne la réponse une fois pour toutes.

---

## 9 · Bien formuler vos demandes

Il n'y a pas de syntaxe à apprendre. Mais une demande vague donne un résultat vague. Une bonne
demande tient en quatre morceaux.

| Le morceau | La question à laquelle il répond | Exemple |
|---|---|---|
| **L'endroit** | Où, précisément ? | « sur la landing page du cours Photographie » |
| **L'action** | Quoi faire ? | « réécris le paragraphe d'introduction » |
| **La contrainte** | Dans quelles limites ? | « en gardant le même titre, maximum 3 phrases, tutoiement » |
| **Le garde-fou** | Que dois-je voir avant que ça parte ? | « montre-moi la version avant / après avant d'enregistrer » |

### Six demandes prêtes à l'emploi

Adaptez les noms, gardez la structure.

**Corriger**
> Relis les cinq derniers articles de mon blog et liste-moi les fautes d'orthographe et de
> grammaire que tu trouves, avec la phrase concernée. Ne corrige rien pour l'instant.

**Uniformiser**
> Sur tout le site, je parle tantôt d'« accompagnement », tantôt de « coaching ». Trouve tous
> les endroits où apparaît « coaching » et remplace-les par « accompagnement ». Montre-moi la
> liste avant d'agir.

**Rédiger**
> Écris un article de blog de 800 mots sur les trois erreurs de débutant en photo de portrait,
> dans le même ton que mes articles existants. Mets-le en brouillon, je le relirai.

**Référencer**
> Vérifie que chacune de mes landing pages a une méta-description. Pour celles qui n'en ont
> pas, propose-m'en une de 150 caractères maximum et attends ma validation.

**Vendre**
> Prépare une offre à −25 % sur mon cours Photographie, valable du 1er au 8 septembre, et
> rédige l'e-mail d'annonce en brouillon. Ne publie rien.

**Ranger**
> Liste mes étiquettes de contacts et dis-moi lesquelles font doublon ou ne sont plus
> utilisées. Propose-moi un ménage, je choisirai.

> **Le réflexe qui change tout**
> Terminez vos demandes par « **montre-moi d'abord** » ou « **ne publie rien** ». Ça ne coûte
> rien, ça vous laisse la main, et ça évite l'immense majorité des mauvaises surprises.

---

## 10 · Les règles de prudence

Cinq habitudes qui suffisent à travailler sereinement.

### 1. Ne comptez pas sur le brouillon les yeux fermés

La règle générale est bien que les modifications arrivent en brouillon, à valider par vous dans
Kajabi. Mais quand un type de contenu n'a pas de brouillon dans Kajabi — c'est le cas des
**publications communautaires** et des **formulaires** — la modification part directement en
ligne. D'où le conseil de laisser la permission « communauté » décochée.

### 2. Vérifiez dans Kajabi, pas dans la conversation

L'assistant vous dit ce qu'il a fait. Ce n'est pas une preuve. Ouvrez Kajabi, regardez la page
concernée. Les deux premières semaines, faites-le à chaque fois.

### 3. Une demande à la fois, au début

Une phrase peut déclencher dix modifications. Tant que vous n'avez pas confiance, demandez une
chose, vérifiez, puis passez à la suivante. Vous saurez ainsi toujours quoi défaire.

### 4. Ne collez jamais de données sensibles dans la conversation

Coordonnées bancaires, mots de passe, listes de clients exportées : rien de tout cela n'a
besoin d'être copié dans le chat. L'assistant accède déjà à ce dont il a besoin, directement et
de façon encadrée.

### 5. Sachez couper l'accès

À tout moment, dans Claude ou Cowork : `Customize › Connectors`, puis **Remove** (ou le menu
« … » à côté du connecteur). Côté Codex, retirez le bloc `[mcp_servers.kajabi]` de votre
fichier de réglages. Et côté Kajabi, vous pouvez révoquer l'autorisation depuis les réglages de
votre compte, dans la section des applications connectées. La coupure est immédiate.

---

## 11 · Quand ça ne marche pas

| Ce que vous constatez | Ce qui se passe | Ce qu'il faut faire |
|---|---|---|
| **L'assistant dit qu'il n'a aucun outil Kajabi** | Le connecteur est éteint dans cette conversation, ou pas activé sur le compte. | Bouton « + » en bas à gauche › Connectors › activez Kajabi. Puis vérifiez que la connexion apparaît bien dans vos réglages. |
| **L'écran d'autorisation Kajabi refuse de s'afficher** | Votre navigateur n'est pas connecté au bon compte Kajabi. | Ouvrez Kajabi dans votre navigateur, connectez-vous au bon compte, laissez l'onglet ouvert, recommencez. |
| **On vous redemande sans arrêt de vous reconnecter** | Plusieurs comptes se marchent dessus, ou l'autorisation a expiré. | Connectez-vous au bon compte, fermez complètement l'assistant, rouvrez-le et réessayez. |
| **Message « connecteurs personnalisés indisponibles »** | Réservé à Codex / ChatGPT : le mode développeur n'est pas activé. | Réglages › Connectors › Advanced, activez « Developer mode » et acceptez l'avertissement. En entreprise, un administrateur doit d'abord l'autoriser. |
| **L'assistant réclame un « site id »** | Il ne sait pas encore sur quel site travailler. | Écrivez simplement « Liste mes sites Kajabi », puis reformulez votre demande. |

> 🛑 **Si une modification vous a échappé**
> Ne demandez pas à l'assistant de « tout remettre comme avant » : il n'a pas forcément gardé
> l'état d'origine. Coupez d'abord l'accès (règle 5 ci-dessus), puis rétablissez le contenu
> depuis Kajabi, en vous appuyant sur son historique de versions quand le type de contenu en
> propose un. Reconnectez ensuite, avec moins de permissions.

---

## 12 · Petit glossaire

Les six mots que vous croiserez, expliqués une fois pour toutes.

**MCP** — La « porte de service » standardisée par laquelle un assistant IA parle à un logiciel
comme Kajabi. Pour vous, c'est juste une adresse à coller une fois.

**Connecteur** — Le nom que Claude et Cowork donnent à une de ces portes une fois installée.
« Ajouter un connecteur Kajabi » = « brancher Kajabi ».

**Serveur distant / Streamable HTTP** — Signifie simplement que le service tourne chez Kajabi,
sur internet, et pas sur votre ordinateur. C'est le type à choisir quand un menu vous le
demande.

**OAuth** — Le mécanisme d'autorisation qui vous évite de donner votre mot de passe. Le même
que lorsque vous vous connectez à un site « avec Google ». C'est lui qui affiche l'écran des
permissions.

**Permission (ou scope)** — Une case à cocher qui ouvre une pièce précise de votre site. Neuf
sont proposées, elles s'activent séparément, et vous pouvez en retirer plus tard.

**Outil (tool)** — Une action unitaire que l'assistant sait déclencher — « lister les sites »,
« modifier un article ». Vous n'avez pas à les connaître : vous décrivez le résultat voulu,
l'assistant choisit les outils.

---

## Sources

Les libellés d'interface peuvent évoluer ; la logique décrite ici, elle, reste valable.

- [Kajabi — Connect Kajabi to Claude or ChatGPT](https://help.kajabi.com/articles/api-integrations/connect-kajabi-to-claude-or-chatgpt)
- [Kajabi — Kajabi MCP](https://www.kajabi.com/product/mcp)
- [Anthropic — Get started with custom connectors using remote MCP](https://support.claude.com/en/articles/11175166-get-started-with-custom-connectors-using-remote-mcp)
- [OpenAI — Codex & MCP](https://learn.chatgpt.com/docs/extend/mcp)
