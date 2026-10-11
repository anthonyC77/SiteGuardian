# Analyse des red flags tarifaires — Microsoft Copilot Studio

*Dernière mise à jour : 14 août 2026*

## Contexte

Copilot Studio est facturé via des **Copilot Credits**, consommés selon les actions des agents. Deux modes d'achat :
- **Pack prépayé** : 200 $/mois pour 25 000 crédits, sans report.
- **Pay-as-you-go** : 0,01 $/crédit via Azure (socle minimum de 500 crédits prépayés requis).

Des packs annuels ("Commit Units") existent à partir de 2 850 $, avec 5 à 20 % de remise et validité 12 mois.

---

## Analyse des red flags

### 1. Crédits opaques indexés sur une complexité non auditée — ✅ Fondé
Le coût dépend de la conception de l'agent et des fonctionnalités mobilisées ; plusieurs types de messages peuvent se cumuler sur une même interaction (ex. tenant graph-grounding : 10 crédits + réponse générative : 2 crédits = 12 crédits pour un seul prompt). Le harnais "GitHub Copilot" facture séparément tokens LLM, outils et harnais lui-même.

### 2. Empilement siège + compteur + gouvernance — ✅ Fondé
Trois briques distinctes : licence M365 Copilot par utilisateur (siège), Copilot Credits (compteur), et coûts Azure OpenAI séparés en BYOM (gouvernance), suivis dans des systèmes différents.

### 3. Travail échoué facturé comme travail fait — ✅ Fondé (surtout harnais GitHub Copilot)
Sur le harnais GitHub Copilot, la facturation s'applique à la construction, aux tests et à l'évaluation — un tour qui échoue peut consommer des crédits. Une salutation proactive sans réponse est facturée même classée "non engagée".

### 4. Pas de report, arrêt sec — ✅ Fondé
Les packs prépayés de 25 000 crédits/mois ne se reportent pas d'un mois sur l'autre. Seuls les Commit Units annuels offrent une validité étendue, avec engagement final sans remboursement.

### 5. Péage sur vos propres données — ⚠️ Partiel (documenté, mais réel)
Interroger vos propres données (tenant graph-grounding) consomme des crédits spécifiques en plus du coût de génération. Documenté publiquement, mais le principe d'un coût marginal sur vos propres données reste réel.

### 6. Pas de compteur consultable en cours de contrat — ❌ Infondé (dashboards existent)
Plusieurs tableaux de bord existent : Power Platform admin center, page Analytics de Copilot Studio, rapport d'usage avec alertes automatiques dès 2 000-3 000 crédits/utilisateur/30 jours, visible dans l'heure.

### 7. Changements unilatéraux rapprochés — ✅ Fondé, très documenté
Au moins 5 changements en 8 mois :

| Date | Changement |
|---|---|
| Sept. 2025 | "Messages" renommés "Copilot Credits", comptage passé au modèle par fonctionnalité |
| Oct. 2025 | Introduction du "Credit P3" |
| Déc. 2025 | Augmentation du quota Dataverse par défaut |
| Fév. 2026 | Introduction du "Microsoft Agent P3" |
| Avr. 2026 | Consolidation documentaire |

Un cabinet de licensing spécialisé qualifie ce rythme de "structurel et non accidentel", recommandant d'anticiper d'autres changements à venir lors de toute signature.

---

## Tableau récapitulatif

| Red flag | Verdict |
|---|---|
| Crédits opaques / complexité non auditée | ✅ Fondé |
| Empilement siège + compteur + gouvernance | ✅ Fondé |
| Travail échoué facturé | ✅ Fondé (surtout harnais GitHub Copilot) |
| Pas de report, arrêt sec | ✅ Fondé |
| Péage sur vos propres données | ⚠️ Partiel (documenté, mais réel) |
| Pas de compteur consultable | ❌ Infondé (dashboards existent) |
| Changements unilatéraux rapprochés | ✅ Fondé, très documenté |

**Score : 5 fondés, 1 partiel, 1 infondé sur 7.**

---

## Sources

- [Microsoft 365 Copilot Pricing – Copilot Studio](https://www.microsoft.com/en-us/microsoft-365-copilot/pricing/copilot-studio)
- [Copilot Studio | Pay as you go pricing (Azure)](https://azure.microsoft.com/en-us/pricing/details/copilot-studio/)
- [FAQ for Copilot Studio billing and licensing (Microsoft Learn)](https://learn.microsoft.com/en-us/microsoft-copilot-studio/faq-billing-licensing)
- [Standard harness licensing (Microsoft Learn)](https://learn.microsoft.com/en-us/microsoft-copilot-studio/billing-licensing)
- [Manage Copilot Studio credits and capacity (Microsoft Learn)](https://learn.microsoft.com/en-us/power-platform/admin/manage-copilot-studio-messages-capacity)
- [Copilot Studio Licensing — April 2026, Explained (LicenseQ)](https://licenseq.com/copilot-studio-licensing/)
- [Copilot Studio Message Pricing & Cost Control 2026 (The Negotiation Experts)](https://thenegotiationexperts.com/blog/copilot-studio-message-pricing-costs/)