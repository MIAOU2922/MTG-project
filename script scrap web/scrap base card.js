// site sur le quelle est recup les cartes vierges
// https://mtg-card-maker.herokuapp.com/
// Copier-coller ce script dans la console du navigateur
// puis appuyer sur Entrée
// Les images seront téléchargées automatiquement


(async () => {
    // Récupérer le canvas
    const canvas = document.getElementById('gameCanvas');
    if (!canvas) return console.error("Canvas introuvable !");

    // Récupérer le select mat-select
    const select = document.querySelector('mat-select[name="cardBorderTypeSelect"]');
    if (!select) return console.error("Select introuvable !");

    // Ouvrir le menu pour récupérer les options
    select.click();

    // Attendre un petit délai pour que l'overlay s'affiche
    await new Promise(r => setTimeout(r, 200));

    // Récupérer toutes les options
    const options = Array.from(document.querySelectorAll('.mat-select-panel .mat-option'));

    console.log(`Found ${options.length} options.`);

    // Fonction pour sauvegarder le canvas
    function saveCanvas(name) {
        const image = canvas.toDataURL('image/png');
        const link = document.createElement('a');
        link.href = image;
        link.download = name + '.png';
        link.click();
    }

    for (let i = 0; i < options.length; i++) {
        const option = options[i];

        // Sélectionner l'option
        option.click();

        // Attendre que le canvas se mette à jour
        await new Promise(r => setTimeout(r, 500)); // ajuster si nécessaire

        // Récupérer le texte de l'option pour le nom
        const name = option.innerText.trim().replace(/\s+/g, '_');

        // Sauvegarder le canvas
        saveCanvas(name);

        // Ré-ouvrir le select si ce n’est pas la dernière option
        if (i < options.length - 1) {
            select.click();
            await new Promise(r => setTimeout(r, 200));
        }
    }

    console.log("Terminé !");
})();
