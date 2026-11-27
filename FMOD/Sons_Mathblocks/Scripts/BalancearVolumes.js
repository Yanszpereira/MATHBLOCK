/* Balanceamento de volume calculado a partir do RMS e pico dos assets usados. */
studio.menu.addMenuItem({
    name: "MathBlocks\\Aplicar balanceamento de volumes",
    execute: function() {
        var volumes = {
            "365822__mountain852__brick-falling-on-brick": 1.0,
            "796080__colinleblancsound__levelbgm": -6.5,
            "Acertou": 1.0,
            "Balancamexendo": 10.0,
            "Balancamexendo 2": 10.0,
            "blockundo": -3.5,
            "BlocoNooutro": 6.5,
            "bocejo2 2": -3.0,
            "carimbo": 0.0,
            "divisao": -4.0,
            "Erro 2": -12.0,
            "ErroOperation": -9.0,
            "esticabloco": 4.5,
            "fase4musica": -5.0,
            "Hover": 6.0,
            "HoverMenu": -1.5,
            "Ilha Parando": -13.5,
            "IlhaFlutuante_ComeçandoMexer": -12.5,
            "IlhaMexendo": -4.5,
            "Massive_floating_sto_#2-1788997194334": -10.5,
            "Massive_floating_sto_#2-1789011122435": -12.0,
            "Massive_floating_sto_#4-1789011124880": -13.0,
            "multiplication": 4.5,
            "MusicaFase1Certa": -5.5,
            "MusicaFase2": 0.0,
            "MusicaFase3": 10.0,
            "passos1": 2.5,
            "PegaBloco": -8.5,
            "piscada": -4.5,
            "Ponteaparecendo": -5.5,
            "Pontesurgindo": -5.0,
            "Pontesurgindo2": -4.5,
            "PortaAbre": 2.5,
            "Pulo": 0.0,
            "Pulo 2": 0.0,
            "Pulo 3": 0.0,
            "Pulo 4": 0.0,
            "quedavoid": 6.0,
            "resultadoteste": -5.0,
            "SaladeAula": 10.0,
            "soma": 1.5,
            "Sombalanca": 10.0,
            "SonsMenu 2": 3.0,
            "SonsMenu 3": 3.0,
            "SonsMenu 4": 3.0,
            "SonsMenu 5": 3.0,
            "SonsMenu 6": 3.0,
            "subtracao": -3.0
        };

        var changed = 0;
        studio.project.model.Event.findInstances().forEach(function(event) {
            if (!Object.prototype.hasOwnProperty.call(volumes, event.name)) return;
            event.groupTracks.forEach(function(track) {
                track.mixerGroup.volume = volumes[event.name];
            });
            changed++;
        });

        studio.project.save();
        studio.system.getText("Balanceamento aplicado em " + changed + " eventos. Projeto salvo.", "OK");
    }
});
