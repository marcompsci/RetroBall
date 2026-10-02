using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Tiny localization layer. English text is the key; <see cref="T"/> returns the Spanish version
    /// when the language is "es". Exact matches first, then known multi-word phrases inside longer
    /// lines (e.g. "NEW RECORD: POINTS"). Anything without a translation stays in English.
    /// Names (teams, leagues, players, the Gold Signal Cup) are proper nouns and aren't translated.
    /// </summary>
    public static class Loc
    {
        public const string English = "en";
        public const string Spanish = "es";
        public static readonly string[] Languages = { English, Spanish };
        public static readonly string[] LanguageNames = { "ENGLISH", "ESPAÑOL" };

        /// <summary>Current language (set from Settings at startup).</summary>
        public static string Language { get; set; } = English;

        public static string Normalize(string lang) => lang == Spanish ? Spanish : English;

        public static string T(string text)
        {
            if (Language != Spanish || string.IsNullOrEmpty(text)) return text;
            if (Es.TryGetValue(text, out var exact)) return exact;
            // Composite lines: translate the known phrases inside them, longest first.
            string result = text;
            foreach (var kv in Phrases)
                if (result.IndexOf(kv.Key, StringComparison.Ordinal) >= 0) result = result.Replace(kv.Key, kv.Value);
            return result;
        }

        public static bool Has(string english) => Es.ContainsKey(english);

        public static IEnumerable<KeyValuePair<string, string>> AllSpanish => Es;

        // ------------------------------------------------------------------ Spanish

        private static readonly Dictionary<string, string> Es = new Dictionary<string, string>
        {
            // Main menu & modes
            ["PLAY"] = "JUGAR", ["RISE MODE"] = "MODO ASCENSO", ["PRACTICE LAB"] = "LABORATORIO", ["LOCKER ROOM"] = "VESTUARIO",
            ["SETTINGS"] = "AJUSTES", ["QUICK CALL"] = "PARTIDO RÁPIDO", ["DAILY CHALLENGE"] = "RETO DIARIO", ["2 PLAYER"] = "2 JUGADORES",
            ["FIRST CALL CLASSIC"] = "FIRST CALL CLASSIC", ["HOW TO PLAY"] = "CÓMO JUGAR", ["KING OF THE COURT"] = "REY DE LA CANCHA",
            ["BACK"] = "ATRÁS", ["TIP OFF"] = "¡A JUGAR!", ["CHANGE OPPONENT"] = "CAMBIAR RIVAL", ["CHANGE TEAM"] = "CAMBIAR EQUIPO",
            ["PICK YOUR TEAM"] = "ELIGE TU EQUIPO", ["DIFFICULTY"] = "DIFICULTAD", ["PLAYER 1"] = "JUGADOR 1", ["PLAYER 2"] = "JUGADOR 2",
            ["ENTER"] = "ENTRAR", ["NEW CLASSIC"] = "NUEVO CLASSIC", ["START"] = "EMPEZAR", ["NEW RUN"] = "NUEVA RACHA",
            ["NEW TO RETROBALL?"] = "¿NUEVO EN RETROBALL?", ["PLAY TUTORIAL"] = "JUGAR TUTORIAL", ["MAYBE LATER"] = "QUIZÁS DESPUÉS",
            ["Learn the controls in about two minutes: move, shoot, pass, call plays, and defend."] =
                "Aprende los controles en unos dos minutos: moverte, tirar, pasar, pedir jugadas y defender.",
            ["Pick a team and an opponent. One game."] = "Elige un equipo y un rival. Un partido.",
            ["Head to head on one device: keyboard or two controllers."] = "Mano a mano en un dispositivo: teclado o dos mandos.",
            ["Two-minute guided tutorial."] = "Tutorial guiado de dos minutos.",
            ["Beat league teams back to back until you lose."] = "Vence a equipos de la liga seguidos hasta que pierdas.",
            ["FREE SHOOT"] = "TIRO LIBRE", ["PASSING TARGETS"] = "OBJETIVOS DE PASE", ["DRIBBLE LANE"] = "CARRIL DE DRIBLE",
            ["3-POINT CONTEST"] = "CONCURSO DE TRIPLES", ["LOCKDOWN"] = "CANDADO",
            ["SAVE RESET"] = "PARTIDA REINICIADA",
            ["Your save file couldn't be read, so a fresh career was started. A backup of the old file was kept."] =
                "No se pudo leer tu partida guardada, así que empezó una carrera nueva. Se guardó una copia del archivo anterior.",

            // In-game controls & callouts
            ["SHOOT"] = "TIRAR", ["PASS"] = "PASAR", ["ASK"] = "PEDIR", ["DEF"] = "DEF", ["CALL"] = "JUGADA", ["STEAL"] = "ROBAR",
            ["JUMP"] = "SALTAR", ["SWITCH"] = "CAMBIO", ["CLEAR"] = "SACAR", ["NO BALL"] = "SIN BALÓN", ["OFFENSE"] = "ATAQUE",
            ["TAKE IT BACK"] = "SÁCALA DEL ARCO", ["SOON"] = "PRONTO",
            ["CHECK BALL"] = "BALÓN AL CENTRO", ["YOUR BALL"] = "TU BALÓN", ["DEFENSE"] = "DEFENSA", ["CLEARED"] = "LIMPIA",
            ["BOARD!"] = "¡REBOTE!", ["PICKED OFF!"] = "¡INTERCEPTADO!", ["STOLEN"] = "ROBADO", ["STEAL!"] = "¡ROBO!",
            ["STRIPPED"] = "TE LA QUITARON", ["BLOCKED!"] = "¡TAPÓN!", ["SENT BACK"] = "TE TAPARON", ["SCREEN SET"] = "BLOQUEO PUESTO",
            ["SHOT CLOCK"] = "RELOJ DE TIRO", ["BOX OUT"] = "CIERRA EL REBOTE", ["HEATING UP"] = "SE CALIENTA", ["ON FIRE!"] = "¡EN LLAMAS!",
            ["CALL A PLAY"] = "PIDE UNA JUGADA", ["PICK & ROLL"] = "PICK & ROLL", ["GIVE & GO"] = "DAR Y IR", ["CLEAR OUT"] = "ABRIR CANCHA",
            ["CANCEL"] = "CANCELAR", ["PAUSED"] = "PAUSA", ["RESUME"] = "SEGUIR", ["QUIT TO MENU"] = "SALIR AL MENÚ",
            ["REPLAY"] = "REPETICIÓN", ["PLAY OF THE GAME"] = "JUGADA DEL PARTIDO", ["NICE!"] = "¡BIEN!",
            ["PLAYER 1  VS  PLAYER 2"] = "JUGADOR 1  VS  JUGADOR 2",
            ["GREEN"] = "VERDE", ["CLEAN LOOK"] = "TIRO LIBRE DE MARCA", ["CONTESTED"] = "PUNTEADO", ["TOO EARLY"] = "MUY PRONTO", ["TOO LATE"] = "MUY TARDE",

            // Post-game
            ["YOU WIN"] = "¡GANASTE!", ["FINAL"] = "FINAL", ["TIE"] = "EMPATE", ["REMATCH"] = "REVANCHA", ["HOME"] = "INICIO",
            ["CONTINUE"] = "CONTINUAR", ["RUN IT BACK"] = "OTRA VEZ", ["NEW PERSONAL BEST!"] = "¡NUEVA MARCA PERSONAL!",
            ["TUTORIAL COMPLETE"] = "TUTORIAL COMPLETADO", ["YOU'RE READY TO PLAY"] = "YA ESTÁS LISTO PARA JUGAR",
            ["CHAMPIONS"] = "CAMPEONES", ["CHAMPIONS!"] = "¡CAMPEONES!", ["CLASSIC CHAMPS"] = "CAMPEONES DEL CLASSIC",
            ["DAILY DONE"] = "RETO CUMPLIDO", ["STATIC SILENCED"] = "STATIC SILENCIADO", ["PLAYER 1 WINS"] = "GANA EL JUGADOR 1",
            ["PLAYER 2 WINS"] = "GANA EL JUGADOR 2", ["STILL KING"] = "SIGUES DE REY", ["DETHRONED"] = "DESTRONADO",

            // Rise hub
            ["PLAY NEXT"] = "SIGUIENTE PARTIDO", ["START NEXT SEASON"] = "EMPEZAR SIGUIENTE TEMPORADA", ["YOUR CREW"] = "TU EQUIPO",
            ["NEXT GAME"] = "SIGUIENTE PARTIDO", ["SEMIFINAL"] = "SEMIFINAL", ["SEMI"] = "SEMI", ["PLAYOFFS"] = "PLAYOFFS",
            ["SEASON COMPLETE"] = "TEMPORADA TERMINADA", ["ENERGY"] = "ENERGÍA", ["CHEM"] = "QUÍMICA", ["RECORD"] = "BALANCE",
            ["CIRCUIT"] = "CIRCUITO", ["CIRCUIT CLEARED"] = "CIRCUITO SUPERADO", ["RIVAL CHALLENGE: NEON STATIC"] = "RETO RIVAL: NEON STATIC",
            ["AVAILABLE"] = "DISPONIBLES", ["DONE"] = "LISTO", ["FREE"] = "GRATIS", ["NICE"] = "GENIAL", ["OK"] = "OK",
            ["LET'S GO"] = "¡VAMOS!",

            // Locker room
            ["PLAYER"] = "JUGADOR", ["CREATE"] = "CREAR", ["TRAIN"] = "ENTRENAR", ["STYLE"] = "ESTILO", ["STATS"] = "ESTAD.",
            ["TROPHY"] = "TROFEOS", ["NICKNAME"] = "APODO", ["SKIN TONE"] = "TONO DE PIEL", ["HAIR"] = "PELO",
            ["HAIR COLOUR"] = "COLOR DE PELO", ["BUILD"] = "COMPLEXIÓN", ["HEIGHT"] = "ALTURA", ["NUMBER"] = "NÚMERO",
            ["STYLE OF PLAY"] = "ESTILO DE JUEGO", ["CREATE PLAYER"] = "CREAR JUGADOR", ["SAVE CHANGES"] = "GUARDAR CAMBIOS",
            ["GO BACK TO ROOK"] = "VOLVER A ROOK", ["USE ROOK?"] = "¿USAR A ROOK?", ["USE ROOK"] = "USAR A ROOK",
            ["BLACK"] = "NEGRO", ["DARK BROWN"] = "CASTAÑO OSCURO", ["BROWN"] = "CASTAÑO", ["BLONDE"] = "RUBIO", ["RED"] = "PELIRROJO",
            ["SLIM"] = "DELGADO", ["STANDARD"] = "NORMAL", ["BROAD"] = "ANCHO", ["SHORT"] = "BAJO", ["AVERAGE"] = "MEDIO", ["TALL"] = "ALTO",
            ["BUY"] = "COMPRAR", ["EQUIP"] = "EQUIPAR", ["MAX"] = "MÁX", ["JERSEY PALETTE"] = "CAMISETA", ["SHOES"] = "ZAPATILLAS",
            ["COURT BANNER"] = "PANCARTA", ["CELEBRATION"] = "CELEBRACIÓN", ["DRIBBLE MOVE"] = "MOVIMIENTO DE DRIBLE",
            ["GAMES"] = "PARTIDOS", ["POINTS"] = "PUNTOS", ["ASSISTS"] = "ASISTENCIAS", ["REBOUNDS"] = "REBOTES", ["STEALS"] = "ROBOS",
            ["BLOCKS"] = "TAPONES", ["FIELD GOALS"] = "TIROS DE CAMPO", ["GREEN RELEASES"] = "LANZAMIENTOS VERDES", ["GREENS"] = "VERDES",
            ["CHAMPIONSHIPS"] = "CAMPEONATOS", ["CLASSIC TITLES"] = "TÍTULOS DEL CLASSIC", ["BIGGEST WIN"] = "MAYOR VICTORIA",
            ["WIN STREAK"] = "RACHA DE VICTORIAS", ["RECORDS (ONE GAME)"] = "RÉCORDS (UN PARTIDO)", ["RISE SEASONS"] = "TEMPORADAS",
            ["RECENT GAMES"] = "PARTIDOS RECIENTES", ["PRACTICE BESTS"] = "MEJORES MARCAS", ["TITLES"] = "TÍTULOS",
            ["VS NEON STATIC"] = "VS NEON STATIC", ["KING STREAK"] = "RACHA DE REY",

            // Settings
            ["AUDIO"] = "AUDIO", ["MUSIC"] = "MÚSICA", ["SFX"] = "EFECTOS", ["FEEL"] = "SENSACIÓN", ["HAPTICS"] = "VIBRACIÓN",
            ["SCREEN SHAKE"] = "TEMBLOR DE PANTALLA", ["ACCESSIBILITY"] = "ACCESIBILIDAD", ["UI SCALE"] = "TAMAÑO DE INTERFAZ",
            ["SMALL"] = "PEQUEÑO", ["DEFAULT"] = "NORMAL", ["LARGE"] = "GRANDE", ["LARGEST"] = "MUY GRANDE",
            ["TEAM PATTERNS"] = "PATRONES DE EQUIPO", ["LEFT-HANDED"] = "ZURDO", ["LARGE BUTTONS"] = "BOTONES GRANDES",
            ["TAP TO SHOOT"] = "TOCAR PARA TIRAR", ["REDUCE MOTION"] = "REDUCIR MOVIMIENTO", ["GAME"] = "JUEGO",
            ["LANGUAGE"] = "IDIOMA", ["RESET SAVE"] = "BORRAR PARTIDA", ["RESET SAVE?"] = "¿BORRAR PARTIDA?", ["RESET"] = "BORRAR",
            ["GAME CENTER"] = "GAME CENTER", ["SIGN IN"] = "INICIAR SESIÓN", ["OPEN GAME CENTER"] = "ABRIR GAME CENTER",
            ["ON"] = "SÍ", ["OFF"] = "NO",
            ["Team patterns give each side a distinct jersey pattern, not just a colour."] =
                "Los patrones dan a cada equipo un dibujo distinto en la camiseta, no solo un color.",
            ["Difficulty changes how fast and how well the AI decides. It never boosts their ratings."] =
                "La dificultad cambia lo rápido y lo bien que decide la IA. Nunca sube sus valoraciones.",
            ["This deletes your career, Rise Mode progress, upgrades, and cosmetics on this device. It can't be undone."] =
                "Esto borra tu carrera, el progreso del Modo Ascenso, las mejoras y los cosméticos en este dispositivo. No se puede deshacer.",
            ["Game Center is only available in the iPhone app."] = "Game Center solo está disponible en la app de iPhone.",

            // Tutorial
            ["MOVE"] = "MUÉVETE", ["JUMP TO CONTEST"] = "SALTA PARA PUNTEAR", ["HIT THE GREEN"] = "ACIERTA EL VERDE", ["ASK FOR IT"] = "PÍDELA", ["YOU'RE READY"] = "YA ESTÁS LISTO",
            ["Drag the stick on the left to move"] = "Arrastra el stick de la izquierda para moverte",
            ["Hold SHOOT, then let go to release"] = "Mantén TIRAR y suelta para lanzar",
            ["Let go when the meter is in the green band"] = "Suelta cuando el medidor esté en la franja verde",
            ["Tap PASS. Aim with the stick"] = "Toca PASAR. Apunta con el stick",
            ["A teammate has it. Tap ASK"] = "La tiene un compañero. Toca PEDIR",
            ["Tap CALL and pick a play"] = "Toca JUGADA y elige una",
            ["Get close to the ball and tap STEAL"] = "Acércate al balón y toca ROBAR",
            ["Tap JUMP to contest a shot"] = "Toca SALTAR para puntear un tiro",
            ["Tutorial complete"] = "Tutorial completado",

            // Rise event cards
            ["Late Practice"] = "Práctica Tardía", ["Your teammate asks to stay after practice and work on timing."] = "Tu compañero pide quedarse después del entrenamiento para trabajar el ritmo.",
            ["Stay and run it back"] = "Quedarse a repetir", ["Rest up tonight"] = "Descansar esta noche",
            ["Court Challenge"] = "Desafío de Cancha", ["A local court challenges your crew to a pickup run for bragging rights."] = "Una cancha del barrio reta a tu equipo a un partido por el orgullo.",
            ["Accept — the crowd will show up"] = "Aceptar: vendrá público", ["Politely pass"] = "Decir que no con educación",
            ["Youth Clinic"] = "Clínica Juvenil", ["The rec centre asks if you'll help run a free youth clinic."] = "El centro recreativo pregunta si ayudas en una clínica gratuita para jóvenes.",
            ["Lace up and teach"] = "Atarse las zapatillas y enseñar", ["Send a signed ball instead"] = "Enviar un balón firmado",
            ["Film Session"] = "Sesión de Video", ["Your crew wants to break down last game's tape together."] = "Tu equipo quiere analizar juntos el video del último partido.",
            ["Pizza and film"] = "Pizza y video", ["Everyone watches alone"] = "Cada uno lo ve solo",
            ["Shootout Night"] = "Noche de Tiro", ["The neighbourhood is hosting a shootout night with a small prize."] = "El barrio organiza una noche de tiro con un pequeño premio.",
            ["Enter the shootout"] = "Participar", ["Cheer from the stands"] = "Animar desde la grada",
            ["Rain Delay"] = "Lluvia", ["A storm rolls in and the gym floor needs a day to dry."] = "Llega una tormenta y el suelo del gimnasio necesita un día para secarse.",
            ["Rest and stretch"] = "Descansar y estirar", ["Shoot in the garage"] = "Tirar en el garaje",
            ["Highlight Tape"] = "Video de Jugadas", ["A local video crew wants to cut a highlight tape of your last game."] = "Un equipo de video local quiere montar las mejores jugadas de tu último partido.",
            ["Let them roll"] = "Que graben", ["Keep it low key"] = "Mejor sin ruido",
            ["Trash Talk"] = "Provocaciones", ["Next week's opponent is talking loud online."] = "El rival de la próxima semana habla mucho en redes.",
            ["Answer on the court"] = "Responder en la cancha", ["Stay quiet"] = "Quedarse callado",
            ["Shoe Drop"] = "Zapatillas Nuevas", ["The corner store got a shipment of fresh sneakers in your size."] = "A la tienda de la esquina le llegaron zapatillas nuevas de tu talla.",
            ["Treat the crew"] = "Invitar al equipo", ["Save the money"] = "Ahorrar el dinero",
            ["Early Bus"] = "Autobús Temprano", ["Road game tomorrow. The bus leaves at dawn."] = "Mañana jugamos fuera. El autobús sale al amanecer.",
            ["Early night"] = "Acostarse temprano", ["Team dinner first"] = "Primero cena de equipo",

            // Badges
            ["FIRST W"] = "PRIMERA VICTORIA", ["Win a game."] = "Gana un partido.", ["GREEN LIGHT"] = "LUZ VERDE", ["Hit a GREEN release."] = "Lanza en VERDE.",
            ["SHARPSHOOTER"] = "FRANCOTIRADOR", ["5 GREEN releases in one game."] = "5 lanzamientos VERDES en un partido.",
            ["BUCKET GETTER"] = "ANOTADOR", ["Score 12 points in one game."] = "Anota 12 puntos en un partido.",
            ["FLOOR GENERAL"] = "BASE GENERAL", ["5 assists in one game."] = "5 asistencias en un partido.",
            ["3 steals in one game."] = "3 robos en un partido.", ["RIM PROTECTOR"] = "PROTECTOR DEL ARO", ["2 blocks in one game."] = "2 tapones en un partido.",
            ["ON A RUN"] = "EN RACHA", ["Win 5 games in a row."] = "Gana 5 partidos seguidos.",
            ["OFF THE BLACKTOP"] = "FUERA DEL ASFALTO", ["Clear The Blacktop Circuit."] = "Supera The Blacktop Circuit.",
            ["GOLD SIGNAL"] = "GOLD SIGNAL", ["Win The Gold Signal Cup."] = "Gana The Gold Signal Cup.",
            ["FIRST CALL"] = "FIRST CALL", ["Win the First Call Classic."] = "Gana el First Call Classic.",
            ["STATIC KILLER"] = "CAZA STATIC", ["Beat Neon Static."] = "Vence a Neon Static.",
            ["RECRUITER"] = "RECLUTADOR", ["Sign a player to your crew."] = "Ficha a un jugador para tu equipo.",
            ["EVERY DAY"] = "TODOS LOS DÍAS", ["7-day Daily Challenge streak."] = "Racha de 7 días de Reto Diario.",
            ["SELF MADE"] = "HECHO A MANO", ["Create your own player."] = "Crea tu propio jugador.",
            ["READY TO CALL"] = "LISTO PARA PEDIR", ["Finish How to Play."] = "Termina Cómo Jugar.",

            // Phase 15: arcade feel, secrets, Arcade Ladder, display
            ["NO CONTINUES NEEDED"] = "SIN CONTINUES", ["Clear the Arcade Ladder."] = "Supera la Escalera Arcade.",
            ["CODE BREAKER"] = "DESCIFRADOR", ["Find every secret code."] = "Encuentra todos los códigos secretos.",
            ["HEAT CHECK!"] = "¡AL ROJO VIVO!", ["HEATING UP"] = "CALENTANDO", ["COOLED OFF"] = "SE ENFRIÓ", ["ALLEY-OOP!"] = "¡ALLEY-OOP!",
            ["DEMO PLAY"] = "DEMOSTRACIÓN", ["DEMO PLAY  ·  TAP OR PRESS ANY BUTTON"] = "DEMOSTRACIÓN  ·  TOCA O PULSA UN BOTÓN",
            ["STICK MOVE · A SHOOT (HOLD) · X PASS · B STEAL · Y CALL · START PAUSE"] =
                "STICK MOVER · A TIRAR (MANTÉN) · X PASAR · B ROBAR · Y JUGADA · START PAUSA",
            ["STAGE CLEAR"] = "FASE SUPERADA", ["CONTINUE?"] = "¿CONTINUAR?", ["GAME OVER"] = "FIN DEL JUEGO", ["LADDER CLEARED"] = "ESCALERA SUPERADA",
            ["THE GLITCH and its court are unlocked!"] = "¡THE GLITCH y su cancha están desbloqueados!",
            ["ARCADE LADDER"] = "ESCALERA ARCADE", ["ARCADE"] = "ARCADE", ["CLEARS"] = "SUPERADAS", ["BEST STAGE"] = "MEJOR FASE",
            ["CONTINUES"] = "CONTINUES", ["YOUR TEAM"] = "TU EQUIPO", ["COURT"] = "CANCHA", ["NEXT"] = "SIGUIENTE", ["BOSS"] = "JEFE",
            ["Six games, each tougher than the last. Lose and use a continue to try the stage again. Lose with none left: GAME OVER."] =
                "Seis partidos, cada uno más duro. Si pierdes, usa un continue para repetir la fase. Sin continues: FIN DEL JUEGO.",
            ["DISPLAY"] = "PANTALLA", ["CRT FILTER"] = "FILTRO CRT", ["SOFT"] = "SUAVE", ["STRONG"] = "FUERTE",
            ["HIGH FRAME RATE"] = "ALTA TASA DE FOTOGRAMAS", ["TITLE DEMO"] = "DEMO EN EL TÍTULO",
            ["CRT adds old-TV scanlines. High frame rate runs at 120 Hz on ProMotion iPhones (uses more battery). Title demo plays an AI game on the title screen when it's left alone."] =
                "CRT añade líneas de televisor antiguo. La alta tasa usa 120 Hz en iPhone ProMotion (gasta más batería). La demo del título juega un partido de la IA si dejas el menú quieto.",
            ["SECRETS"] = "SECRETOS", ["ENTER A CODE"] = "INTRODUCIR CÓDIGO", ["SECRET CODES"] = "CÓDIGOS SECRETOS",
            ["Enter four symbols. Earn hints by playing."] = "Introduce cuatro símbolos. Gana pistas jugando.",
            ["NOTHING HAPPENED..."] = "NO PASÓ NADA...", ["UNLOCKED!"] = "¡DESBLOQUEADO!", ["CLOSE"] = "CERRAR", ["Codes found:"] = "Códigos encontrados:",
            ["BIG HEADS"] = "CABEZONES", ["RAINBOW BALL"] = "BALÓN ARCOÍRIS", ["ALWAYS HOT"] = "SIEMPRE ARDIENDO",
            ["Everyone gets a giant head."] = "Todos tienen una cabeza gigante.", ["The ball cycles through colours."] = "El balón cambia de colores.",
            ["Unlocks the hidden Pixel Void court."] = "Desbloquea la cancha oculta Pixel Void.", ["Unlocks the secret Cartridge Kids crew."] = "Desbloquea el equipo secreto Cartridge Kids.",
            ["Your player starts every game heated up."] = "Tu jugador empieza cada partido al rojo vivo.",
            ["Win 3 straight in King of the Court."] = "Gana 3 seguidos en Rey de la Cancha.", ["Reach a 3-day Daily Challenge streak."] = "Logra una racha de 3 días de Reto Diario.",
            ["Clear the Arcade Ladder."] = "Supera la Escalera Arcade.", ["Win 10 games."] = "Gana 10 partidos.", ["Hit 50 green releases."] = "Consigue 50 tiros perfectos.",
            ["Enter codes in Settings ▸ SECRETS."] = "Introduce códigos en Ajustes ▸ SECRETOS.",
            ["KING STREAK"] = "RACHA DE REY", ["LADDER CLEARS"] = "ESCALERAS SUPERADAS",
        };

        /// <summary>Phrases translated inside longer composite lines (longest first).</summary>
        private static readonly List<KeyValuePair<string, string>> Phrases = BuildPhrases();

        private static List<KeyValuePair<string, string>> BuildPhrases()
        {
            string[] keys =
            {
                "PLAYER OF THE GAME", "NEW RECORD", "DAILY COMPLETE!", "NEON STATIC BEATEN!", "TITLES WON", "STANDINGS",
                "STARTING OVR", "PUT IN SPOT", "STEP ", "NICE!", "DAILY:", "SLAM!", "SWISH", "BADGE:", "CHAMPION:", "NEXT:",
                "EDITION", "SEASON ", "WEEK ", "STREAK", "BADGES", "DIFFICULTY", "SPOT ", "SEMIFINAL", "FINAL", "BEST",
                "SECRET CODE HINT FOUND!", "SECRET CODES", "Continues left:", "Next: stage", "Reached stage", "Try the stage again.",
            };
            var es = new Dictionary<string, string>
            {
                ["PLAYER OF THE GAME"] = "JUGADOR DEL PARTIDO", ["NEW RECORD"] = "NUEVO RÉCORD", ["DAILY COMPLETE!"] = "¡RETO CUMPLIDO!",
                ["NEON STATIC BEATEN!"] = "¡NEON STATIC VENCIDO!", ["TITLES WON"] = "TÍTULOS GANADOS", ["STANDINGS"] = "CLASIFICACIÓN",
                ["STARTING OVR"] = "VALORACIÓN INICIAL", ["PUT IN SPOT"] = "PONER EN PUESTO", ["STEP "] = "PASO ", ["NICE!"] = "¡BIEN!",
                ["DAILY:"] = "RETO:", ["SLAM!"] = "¡MATE!", ["SWISH"] = "¡LIMPIA!", ["BADGE:"] = "INSIGNIA:", ["CHAMPION:"] = "CAMPEÓN:",
                ["NEXT:"] = "SIGUIENTE:", ["EDITION"] = "EDICIÓN", ["SEASON "] = "TEMPORADA ", ["WEEK "] = "SEMANA ", ["STREAK"] = "RACHA",
                ["BADGES"] = "INSIGNIAS", ["DIFFICULTY"] = "DIFICULTAD", ["SPOT "] = "PUESTO ", ["SEMIFINAL"] = "SEMIFINAL",
                ["FINAL"] = "FINAL", ["BEST"] = "MEJOR",
                ["SECRET CODE HINT FOUND!"] = "¡PISTA DE CÓDIGO SECRETO!", ["SECRET CODES"] = "CÓDIGOS SECRETOS",
                ["Continues left:"] = "Continues restantes:", ["Next: stage"] = "Siguiente: fase", ["Reached stage"] = "Llegaste a la fase",
                ["Try the stage again."] = "Repite la fase.",
            };
            var list = new List<KeyValuePair<string, string>>();
            foreach (var k in keys) list.Add(new KeyValuePair<string, string>(k, es[k]));
            list.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
            return list;
        }
    }
}
