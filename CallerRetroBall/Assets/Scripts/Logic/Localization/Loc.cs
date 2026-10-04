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
            ["NEW TO RETRO HOOPS?"] = "¿NUEVO EN RETRO HOOPS?", ["PLAY TUTORIAL"] = "JUGAR TUTORIAL", ["MAYBE LATER"] = "QUIZÁS DESPUÉS",
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
            ["This deletes your career, Rise Mode progress, upgrades, and cosmetics on this device and in iCloud. It can't be undone."] =
                "Esto borra tu carrera, el progreso del Modo Ascenso, las mejoras y los cosméticos en este dispositivo y en iCloud. No se puede deshacer.",
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

            // Phase 16: season 2, create-a-team, new modes
            ["SUNDOWN SETTLED"] = "ATARDECER CERRADO", ["Beat the Sundown Syndicate."] = "Vence al Sundown Syndicate.",
            ["TWO-TIME"] = "BICAMPEÓN", ["Win The Gold Signal Cup twice."] = "Gana la Gold Signal Cup dos veces.",
            ["SKY HOOKUP"] = "CONEXIÓN AÉREA", ["10 alley-oops (thrown or finished)."] = "10 alley-oops (pasados o rematados).",
            ["HEAT CHECK"] = "AL ROJO VIVO", ["Heat up 10 times."] = "Ponte al rojo vivo 10 veces.",
            ["YOUR COLORS"] = "TUS COLORES", ["Create your own team."] = "Crea tu propio equipo.",
            ["TEAM"] = "EQUIPO", ["TEAM NAME"] = "NOMBRE DEL EQUIPO", ["CITY (OPTIONAL)"] = "CIUDAD (OPCIONAL)",
            ["SHORT NAME (SCOREBOARD)"] = "NOMBRE CORTO (MARCADOR)", ["JERSEY"] = "CAMISETA", ["TRIM"] = "RIBETE", ["ACCENT"] = "DETALLE",
            ["SHORTS"] = "PANTALÓN", ["SHOES"] = "ZAPATILLAS", ["JERSEY PATTERN"] = "DISEÑO DE CAMISETA", ["LOGO SHAPE"] = "FORMA DEL ESCUDO",
            ["LOGO ICON"] = "ICONO DEL ESCUDO", ["HOME COURT"] = "CANCHA LOCAL", ["WEAR IT IN RISE MODE"] = "USARLO EN MODO ASCENSO",
            ["CREATE TEAM"] = "CREAR EQUIPO",
            ["1-ON-1"] = "1 CONTRA 1", ["CALLER CUP"] = "CALLER CUP", ["SHOOTOUT"] = "DUELO DE TRIPLES", ["VS"] = "VS",
            ["Just you and their best. First to 11."] = "Solo tú contra su mejor jugador. Primero a 11.",
            ["Your player against their leader. Teammates sit out. First to 11 or two minutes."] =
                "Tu jugador contra su líder. Los compañeros descansan. Primero a 11 o dos minutos.",
            ["Eight teams, three rounds, one cup. You're the eighth seed. Lose once and you're out."] =
                "Ocho equipos, tres rondas, una copa. Eres el octavo cabeza de serie. Si pierdes, quedas fuera.",
            ["TITLES"] = "TÍTULOS", ["NEW CUP"] = "NUEVA COPA", ["QUARTERFINAL"] = "CUARTOS DE FINAL", ["CUP CHAMPIONS"] = "CAMPEONES DE COPA",
            ["YOU WIN THE SHOOTOUT"] = "¡GANASTE EL DUELO!", ["CPU WINS"] = "GANA LA CPU",
            ["DEFENSE:"] = "DEFENSA:", ["FULL PRESSURE"] = "PRESIÓN TOTAL", ["PACK THE PAINT"] = "CERRAR LA PINTURA", ["ZONE"] = "ZONA",
            ["MAN-TO-MAN"] = "INDIVIDUAL",
            ["CALLER CUP TITLES"] = "TÍTULOS DE CALLER CUP", ["SHOOTOUT WINS"] = "DUELOS GANADOS", ["ALLEY-OOPS"] = "ALLEY-OOPS",
            ["HEAT CHECKS"] = "VECES AL ROJO VIVO",
            ["PARTY GAMES"] = "JUEGOS DE FIESTA", ["H-O-R-S-E VS CPU"] = "H-O-R-S-E CONTRA CPU", ["H-O-R-S-E VS FRIEND"] = "H-O-R-S-E CONTRA AMIGO",
            ["21"] = "21", ["AROUND THE WORLD"] = "LA VUELTA AL MUNDO", ["H-O-R-S-E"] = "H-O-R-S-E",
            ["H-O-R-S-E, 21, Around the World, and the Shootout."] = "H-O-R-S-E, 21, La vuelta al mundo y el Duelo de Triples.",
            ["Pass the phone: P1 and P2 take turns with the same player."] = "Pásense el teléfono: J1 y J2 se turnan con el mismo jugador.",
            ["1-on-1 to exactly 21. Make it, take it. Go over and you bust back to 13."] =
                "1 contra 1 a 21 exactos. Quien anota, repite. Si te pasas, vuelves a 13.",
            ["SHARE HIGHLIGHT"] = "COMPARTIR JUGADA", ["SAVING HIGHLIGHT..."] = "GUARDANDO JUGADA...",
            ["COULDN'T RECORD THE HIGHLIGHT"] = "NO SE PUDO GRABAR LA JUGADA", ["COULDN'T SAVE THE HIGHLIGHT"] = "NO SE PUDO GUARDAR LA JUGADA",
            ["DRAFT DAY"] = "DÍA DEL DRAFT", ["DRAFT PICK WAITING"] = "ELECCIÓN DE DRAFT PENDIENTE", ["LEAGUE HISTORY"] = "HISTORIA DE LA LIGA",
            ["OFF-SEASON"] = "PRETEMPORADA", ["YEAR"] = "AÑO", ["LATER"] = "DESPUÉS", ["CHAMPIONS"] = "CAMPEONES", ["MOST TITLES"] = "MÁS TÍTULOS",
            ["HALL OF FAME"] = "SALÓN DE LA FAMA", ["RIVAL CHALLENGE:"] = "DESAFÍO RIVAL:",
            ["Pick one prospect. They join your crew for free and grow every season they play."] =
                "Elige un prospecto. Se une a tu equipo gratis y mejora cada temporada que juega.",
            ["No champions yet. Finish a Rise season."] = "Aún no hay campeones. Termina una temporada de Ascenso.",
            ["Empty for now. Legends retire after a few seasons."] = "Vacío por ahora. Las leyendas se retiran tras varias temporadas.",
            // Phase 18
            ["TIDE TURNER"] = "CAMBIO DE MAREA", ["Beat the Midnight Tide."] = "Vence a Midnight Tide.",
            ["THREE-PEAT"] = "TRICAMPEÓN", ["Win The Gold Signal Cup three times."] = "Gana la Gold Signal Cup tres veces.",
            ["DYNASTY"] = "DINASTÍA", ["Play five Rise seasons."] = "Juega cinco temporadas de Ascenso.",
            ["POCKET GREEN"] = "VERDE DE BOLSILLO", ["SKY HIGH"] = "POR LAS NUBES",
            ["Four-shade green screen, like an old handheld."] = "Pantalla verde de cuatro tonos, como una consola portátil antigua.",
            ["Everybody jumps twice as high (looks only)."] = "Todos saltan el doble de alto (solo visual).",
            ["Win a game of H-O-R-S-E."] = "Gana una partida de H-O-R-S-E.", ["Throw or finish 5 alley-oops."] = "Pasa o remata 5 alley-oops.",
            ["SHOW FPS"] = "MOSTRAR FPS", ["COACH TIPS"] = "CONSEJOS DE LA ENTRENADORA", ["COACH:"] = "ENTRENADORA:",
            ["Hold SHOOT, release when the meter hits the gold line."] = "Mantén TIRAR y suelta cuando el medidor llegue a la línea dorada.",
            ["A bit early! Wait for the gold line before you let go."] = "¡Un poco pronto! Espera la línea dorada antes de soltar.",
            ["Too late! Let go as soon as the meter reaches gold."] = "¡Muy tarde! Suelta en cuanto el medidor llegue al dorado.",
            ["After a steal or a board, take it back beyond the arc first."] = "Tras un robo o un rebote, sal primero detrás del arco.",
            ["On defense: STEAL near the ball, JUMP when they shoot."] = "En defensa: ROBA cerca del balón, SALTA cuando tiren.",
            ["Gold arrow? Pass now for the alley-oop!"] = "¿Flecha dorada? ¡Pasa ya para el alley-oop!",
            ["One more make and you're HEATING UP."] = "Una canasta más y te CALIENTAS.",
            ["Shot clock's low. Get a shot up!"] = "Se acaba la posesión. ¡Tira ya!",
            ["CUP RUN"] = "CAMPEÓN DE COPA", ["Win the Caller Cup."] = "Gana la Caller Cup.",
            ["SHOOTOUT STAR"] = "ESTRELLA DEL DUELO", ["Win a Shootout."] = "Gana un Duelo de Triples.",
            ["Your team is your player and your crew in your colours. Pick it in Quick Call, King of the Court, the Arcade Ladder, and 2 Player."] =
                "Tu equipo es tu jugador y tu grupo con tus colores. Elígelo en Partido Rápido, Rey de la Cancha, la Escalera Arcade y 2 Jugadores.",
            ["RED"] = "ROJO", ["ORANGE"] = "NARANJA", ["GOLD"] = "ORO", ["LIME"] = "LIMA", ["GREEN"] = "VERDE", ["TEAL"] = "TURQUESA",
            ["SKY"] = "CELESTE", ["BLUE"] = "AZUL", ["NAVY"] = "MARINO", ["PURPLE"] = "MORADO", ["PINK"] = "ROSA", ["MAROON"] = "GRANATE",
            ["BROWN"] = "MARRÓN", ["SILVER"] = "PLATA", ["WHITE"] = "BLANCO", ["BLACK"] = "NEGRO",
            ["SOLID"] = "LISO", ["STRIPES"] = "RAYAS", ["DOTS"] = "PUNTOS", ["CHEVRONS"] = "GALONES", ["CHECKER"] = "CUADROS",
            ["DIAGONAL"] = "DIAGONAL", ["RINGS"] = "ANILLOS", ["CROSS"] = "CRUZ",
            ["CIRCLE"] = "CÍRCULO", ["SHIELD"] = "ESCUDO", ["DIAMOND"] = "ROMBO", ["HEXAGON"] = "HEXÁGONO", ["BADGE"] = "PLACA",
            ["BOLT"] = "RAYO", ["WAVE"] = "OLA", ["PAW"] = "PATA", ["TREE"] = "ÁRBOL", ["COMET"] = "COMETA", ["DUNE"] = "DUNA",
            ["OWL"] = "BÚHO", ["CROWN"] = "CORONA", ["BALL"] = "BALÓN", ["SIGNAL"] = "SEÑAL", ["CRANE"] = "GRULLA",

            // Phase 19: Season 4, accessibility, iCloud, 2 Player.
            ["GROUNDED"] = "ATERRIZADOS", ["Beat the Paper Cranes."] = "Vence a los Paper Cranes.",
            ["NO RIVALS LEFT"] = "SIN RIVALES", ["Beat all four rival crews."] = "Vence a los cuatro equipos rivales.",
            ["COUCH RIVALS"] = "RIVALES DE SOFÁ", ["Play a 2 Player game."] = "Juega una partida de 2 jugadores.",
            ["FOUR CUPS"] = "CUATRO COPAS", ["Win The Gold Signal Cup four times."] = "Gana la Gold Signal Cup cuatro veces.",
            ["RIVAL DOWN"] = "RIVAL DERROTADO", ["They win this one. They'll be back next season."] = "Esta vez ganan ellos. Volverán la próxima temporada.",
            ["COLOR FILTER"] = "FILTRO DE COLOR", ["RED-GREEN"] = "ROJO-VERDE", ["BLUE-YELLOW"] = "AZUL-AMARILLO",
            ["Color filter changes the shot meter colors and gives the away team its other kit when the two teams would look alike."] =
                "El filtro de color cambia los colores del medidor de tiro y da al equipo visitante su otra equipación cuando los dos equipos se parecen.",
            ["CAPTIONS"] = "SUBTÍTULOS",
            ["Captions show the announcer's calls on screen. They're also on when Closed Captions is on in iOS Settings. VoiceOver reads the menus."] =
                "Los subtítulos muestran las frases del locutor en pantalla. También se activan con los subtítulos de iOS. VoiceOver lee los menús.",
            ["ICLOUD"] = "ICLOUD", ["ICLOUD SYNC"] = "SINCRONIZAR CON ICLOUD",
            ["iCloud sync is only available in the iPhone app."] = "La sincronización con iCloud solo está disponible en la app de iPhone.",
            ["Keeps your career in your own iCloud, so a new iPhone picks up where you left off. Settings stay per device."] =
                "Guarda tu carrera en tu propio iCloud, así un iPhone nuevo sigue donde lo dejaste. Los ajustes son de cada dispositivo.",
            ["Sign in to iCloud in the iPhone Settings app to sync your career."] = "Inicia sesión en iCloud en la app Ajustes del iPhone para sincronizar tu carrera.",
            ["WELCOME BACK"] = "BIENVENIDO DE NUEVO", ["Your career was loaded from iCloud."] = "Tu carrera se cargó desde iCloud.",
            ["NEWER CAREER IN ICLOUD"] = "CARRERA MÁS NUEVA EN ICLOUD", ["LOAD FROM ICLOUD"] = "CARGAR DE ICLOUD", ["KEEP THIS ONE"] = "QUEDARME CON ESTA",
            ["FULL COURT"] = "CANCHA COMPLETA", ["FULL COURT  5 ON 5"] = "CANCHA COMPLETA  5 CONTRA 5",
            ["5 on 5, both baskets, 2s and 3s. Four minutes."] = "5 contra 5, las dos canastas, dobles y triples. Cuatro minutos.",
            ["Pick a team and an opponent. One game. Score and it's still your ball."] = "Elige equipo y rival. Un partido. Si anotas, el balón sigue siendo tuyo.",
            ["Five on five, end to end. Inside the arc is 2, outside is 3. After a basket the other team inbounds and brings it up. Four minutes; 20-second shot clock."] =
                "Cinco contra cinco, de canasta a canasta. Dentro del arco vale 2 y fuera 3. Tras una canasta, el otro equipo saca de fondo y sube el balón. Cuatro minutos; 20 segundos de posesión.",
            // Phase 20: Kit Studio, Holiday Games, Full Court rules.
            ["KIT"] = "EQUIPO", ["WEAR IN GAMES"] = "USAR EN PARTIDOS", ["AWAY KIT ON CLASH"] = "VISITANTE SI CHOCAN",
            ["RANDOMIZE"] = "AL AZAR", ["UNDO"] = "DESHACER", ["NAME"] = "NOMBRE", ["JERSEY"] = "CAMISETA", ["TRIM"] = "RIBETE",
            ["ACCENT"] = "DETALLE", ["CUT"] = "CORTE", ["COLLAR"] = "CUELLO", ["SIDE STRIPES"] = "FRANJAS LATERALES", ["CHEST"] = "PECHO",
            ["PATTERN"] = "DISEÑO", ["SHORTS"] = "SHORTS", ["SHORTS TRIM"] = "RIBETE DEL SHORT", ["LENGTH"] = "LARGO",
            ["SIDE STRIPE"] = "FRANJA LATERAL", ["WAISTBAND"] = "CINTURA", ["SHOES"] = "TENIS", ["SOLE"] = "SUELA", ["LACES"] = "CORDONES",
            ["STRIPE"] = "FRANJA", ["SHOE STRIPE"] = "FRANJA DEL TENIS", ["HEIGHT"] = "ALTURA", ["PRESETS"] = "PREAJUSTES",
            ["TEAM COLORS"] = "COLORES DEL EQUIPO", ["SHARE"] = "COMPARTIR", ["SHARE KIT"] = "COMPARTIR EQUIPO",
            ["ENTER CODE"] = "METER CÓDIGO", ["TRADING CARD"] = "CROMO", ["SAVE KITS"] = "GUARDAR EQUIPOS",
            ["UNSAVED CHANGES"] = "CAMBIOS SIN GUARDAR", ["KITS SAVED"] = "EQUIPOS GUARDADOS", ["LOCKED"] = "BLOQUEADO",
            ["HOME"] = "LOCAL", ["AWAY"] = "VISITANTE", ["ALT"] = "ALTERNO",
            ["TANK"] = "SIN MANGAS", ["TEE"] = "CAMISETA", ["LONG SLEEVE"] = "MANGA LARGA", ["V-NECK"] = "CUELLO V", ["CREW"] = "REDONDO",
            ["NONE"] = "NINGUNO", ["SINGLE"] = "SENCILLA", ["DOUBLE"] = "DOBLE", ["LOGO"] = "LOGO", ["BAND"] = "BANDA", ["NUMBER"] = "NÚMERO",
            ["CLASSIC"] = "CLÁSICO", ["LONG"] = "LARGO", ["SHORT"] = "CORTO", ["LOW"] = "BAJO", ["MID"] = "MEDIO", ["HIGH"] = "ALTO",
            ["RED"] = "ROJO", ["GREEN"] = "VERDE", ["BLUE"] = "AZUL", ["DONE"] = "LISTO",
            ["SHARE YOUR KIT"] = "COMPARTE TU EQUIPO", ["COPY CODE"] = "COPIAR CÓDIGO", ["CLOSE"] = "CERRAR",
            ["Scan with an iPhone camera to open it in Retro Hoops, or send the code."] = "Escanéalo con la cámara del iPhone para abrirlo en Retro Hoops, o envía el código.",
            ["ENTER A KIT CODE"] = "METE UN CÓDIGO DE EQUIPO", ["PASTE"] = "PEGAR", ["LOAD KIT"] = "CARGAR EQUIPO",
            ["Paste a code a friend sent you (or a retrohoops://kit/ link)."] = "Pega el código que te mandó un amigo (o un enlace retroball://kit/).",
            ["That code doesn't read. Check it and try again."] = "Ese código no se puede leer. Revísalo e inténtalo otra vez.",
            ["SHARE CARD"] = "COMPARTIR CROMO", ["A FRIEND'S KIT"] = "EL EQUIPO DE UN AMIGO", ["OPEN KIT STUDIO"] = "ABRIR ESTUDIO", ["LATER"] = "MÁS TARDE",
            ["Someone shared a Retro Hoops kit with you. Open it in the Kit Studio?"] = "Alguien compartió un equipo de Retro Hoops contigo. ¿Abrirlo en el estudio?",
            ["Your player and crew wear your kit in every game with your team. Some styles unlock as you play."] =
                "Tu jugador y tu grupo usan tu equipación en todos los partidos con tu equipo. Algunos estilos se desbloquean jugando.",
            ["AWAY KIT ON: COLORS CLASHED"] = "EQUIPO VISITANTE: LOS COLORES CHOCABAN",
            ["Win 10 games."] = "Gana 10 partidos.", ["Hit 25 GREEN releases."] = "Logra 25 lanzamientos VERDES.",
            ["Throw or finish 3 alley-oops."] = "Lanza o remata 3 alley-oops.", ["Heat up 3 times."] = "Ponte al rojo vivo 3 veces.",
            ["Play a Full Court game."] = "Juega un partido de cancha completa.", ["Find 3 secret codes."] = "Encuentra 3 códigos secretos.",
            ["HOLIDAY GAMES"] = "PARTIDOS FESTIVOS", ["Christmas, Halloween, Easter and Fourth of July courts."] = "Canchas de Navidad, Halloween, Pascua y 4 de julio.",
            ["CHRISTMAS GAME"] = "PARTIDO DE NAVIDAD", ["HALLOWEEN GAME"] = "PARTIDO DE HALLOWEEN", ["EASTER GAME"] = "PARTIDO DE PASCUA",
            ["FOURTH OF JULY GAME"] = "PARTIDO DEL 4 DE JULIO", ["IN SEASON NOW"] = "¡ES TEMPORADA!",
            ["Snow on the blacktop and lights on the fence."] = "Nieve en la cancha y luces en la reja.",
            ["Jack-o'-lanterns, bats, and a big orange moon."] = "Calabazas, murciélagos y una gran luna naranja.",
            ["Spring grass and painted eggs along the baseline."] = "Pasto de primavera y huevos pintados junto a la línea de fondo.",
            ["Stars, stripes, and fireworks over the stands."] = "Estrellas, franjas y fuegos artificiales sobre las gradas.",
            ["BACKCOURT!"] = "¡CAMPO ATRÁS!", ["8 SECONDS!"] = "¡8 SEGUNDOS!", ["5 SECONDS!"] = "¡5 SEGUNDOS!",
            ["INBOUND: PASS IT IN"] = "SAQUE: PÁSALA ADENTRO", ["INBOUND"] = "SAQUE", ["WAIT"] = "ESPERA", ["PASS IT IN"] = "PÁSALA ADENTRO",
            ["Your KIT tab design is what your team wears. Jerseys and shoes you own here show up there as presets."] =
                "Tu diseño de la pestaña EQUIPO es lo que viste tu equipo. Las camisetas y tenis que tengas aquí aparecen allí como preajustes.",
            ["SHOOTOUT VS FRIEND"] = "DUELO DE TRIPLES VS AMIGO",
            ["Pass the phone: P1 sets a 60-second score, P2 tries to beat it."] = "Pásate el teléfono: J1 marca una puntuación en 60 segundos y J2 intenta superarla.",
            ["P2: GO"] = "J2: ¡VAMOS!", ["NEW DUEL"] = "NUEVO DUELO", ["PASS THE PHONE TO P2"] = "PÁSALE EL TELÉFONO A J2", ["TIE GAME"] = "EMPATE",
            ["P1: SET THE SCORE"] = "J1: MARCA LA PUNTUACIÓN", ["P1 WINS THE SHOOTOUT"] = "J1 GANA EL DUELO", ["P2 WINS THE SHOOTOUT"] = "J2 GANA EL DUELO",
            ["Head to head on one iPhone: lay it flat between you, or use controllers."] = "Cara a cara en un iPhone: déjalo plano entre los dos, o usa mandos.",
            ["One iPhone: lay it flat between you. P1 plays from the bottom edge, P2 from the top.\n" +
             "Controllers: with two, P1 gets the first; with one, it's P2's (P1 uses touch).\n" +
             "Keyboard: P1 WASD · K J L C, P2 arrows · Num1 Num2 Num3 Num0."] =
                "Un iPhone: déjalo plano entre los dos. J1 juega desde el borde de abajo y J2 desde el de arriba.\n" +
                "Mandos: con dos, J1 usa el primero; con uno, es de J2 (J1 usa la pantalla).\n" +
                "Teclado: J1 WASD · K J L C, J2 flechas · Num1 Num2 Num3 Num0.",
            ["Optional. Posts your best marks to leaderboards and unlocks achievements. The game works the same without it."] =
                "Opcional. Publica tus mejores marcas en las clasificaciones y desbloquea logros. El juego funciona igual sin él.",
            // Phase 22: Legacy, The Park, Tournament Builder, menu sections
            ["PLAY NOW"] = "JUGAR YA", ["CAREERS"] = "CARRERAS", ["EVENTS"] = "EVENTOS", ["WITH FRIENDS"] = "CON AMIGOS",
            ["LEGACY"] = "LEGADO", ["START LEGACY"] = "EMPEZAR LEGADO", ["SKILLS"] = "HABILIDADES", ["SPONSORS"] = "PATROCINADORES",
            ["THE PARK"] = "EL PARQUE", ["CALL OUT"] = "RETAR", ["TOURNAMENT BUILDER"] = "CREADOR DE TORNEOS", ["RANDOM FIELD"] = "EQUIPOS AL AZAR",
            ["ANKLES!"] = "¡TOBILLOS!", ["GOT YOU!"] = "¡TE TENGO!", ["RECRUITING"] = "RECLUTAMIENTO", ["DRAFT NIGHT"] = "NOCHE DEL DRAFT",
            ["HEAR YOUR NAME"] = "ESCUCHA TU NOMBRE", ["OFF-SEASON"] = "RECESO", ["RETIRE"] = "RETIRARSE", ["HALL OF FAME"] = "SALÓN DE LA FAMA",
            ["LEARN"] = "APRENDER", ["LEARNED"] = "APRENDIDA", ["HIRE"] = "CONTRATAR", ["COMMIT"] = "COMPROMETERSE", ["DECLARE"] = "DECLARARSE",
            ["NEW LEGACY"] = "NUEVO LEGADO", ["START OVER"] = "EMPEZAR DE NUEVO", ["ABANDON"] = "ABANDONAR", ["NEW TOURNAMENT"] = "NUEVO TORNEO",
            ["SIM GAME (HALF XP)"] = "SIMULAR (MITAD DE XP)", ["LOADING"] = "CARGANDO",
            // Phase 21: Franchise, All-Star Weekend, Court Builder, Music Player
            ["FRANCHISE"] = "FRANQUICIA", ["ROSTER"] = "PLANTILLA", ["TRADE"] = "TRASPASO",
            ["LEAGUE"] = "LIGA", ["MARKET"] = "MERCADO", ["DRAFT"] = "DRAFT",
            ["HISTORY"] = "HISTORIA", ["PAYROLL"] = "NÓMINA", ["CAP ROOM"] = "ESPACIO SALARIAL",
            ["REGULAR SEASON"] = "TEMPORADA REGULAR", ["RE-SIGN PLAYERS"] = "RENOVAR JUGADORES", ["FREE AGENCY"] = "AGENCIA LIBRE",
            ["PRESEASON"] = "PRETEMPORADA", ["PLAY GAME"] = "JUGAR PARTIDO", ["SIM GAME"] = "SIMULAR PARTIDO",
            ["SIM TO PLAYOFFS"] = "SIMULAR HASTA PLAYOFFS", ["SIM PLAYOFFS"] = "SIMULAR PLAYOFFS", ["START FRANCHISE"] = "EMPEZAR FRANQUICIA",
            ["CHANGE CLUB"] = "CAMBIAR CLUB", ["NEW FRANCHISE"] = "NUEVA FRANQUICIA", ["PROPOSE TRADE"] = "PROPONER TRASPASO",
            ["NEXT TEAM"] = "SIGUIENTE EQUIPO", ["YOU SEND"] = "ENVÍAS", ["YOU GET"] = "RECIBES",
            ["FREE AGENTS"] = "AGENTES LIBRES", ["SIGN"] = "FICHAR", ["RE-SIGN"] = "RENOVAR",
            ["STARTERS (YOU CONTROL #1)"] = "TITULARES (CONTROLAS AL #1)", ["BENCH"] = "BANCA", ["TOP SCORERS"] = "MÁXIMOS ANOTADORES",
            ["SCOUT"] = "OJEAR", ["AWARDS"] = "PREMIOS", ["TRANSACTIONS"] = "MOVIMIENTOS",
            ["FINISH FREE AGENCY"] = "TERMINAR AGENCIA LIBRE", ["DONE: OPEN FREE AGENCY"] = "LISTO: ABRIR AGENCIA LIBRE", ["GO TO THE DRAFT"] = "IR AL DRAFT",
            ["THE DRAFT"] = "EL DRAFT", ["MISSED PLAYOFFS"] = "SIN PLAYOFFS", ["LOST SEMI"] = "PERDIÓ SEMIFINAL",
            ["LOST FINAL"] = "PERDIÓ LA FINAL", ["ALL-STAR CONTESTS"] = "CONCURSOS DE ESTRELLAS", ["ALL-STAR WEEKEND"] = "FIN DE SEMANA DE ESTRELLAS",
            ["DUNK CONTEST"] = "CONCURSO DE CLAVADAS", ["ALL-STAR GAME"] = "PARTIDO DE ESTRELLAS", ["JAM!"] = "¡MÁTALA!",
            ["SLAM IT!"] = "¡CLÁVALA!", ["ENTER THE COMBO!"] = "¡METE EL COMBO!", ["BLOWN!"] = "¡FALLADA!",
            ["TOO SLOW"] = "MUY LENTO", ["WRONG MOVE"] = "MOVIMIENTO EQUIVOCADO", ["TRY AGAIN"] = "OTRA VEZ",
            ["ROUND ONE"] = "PRIMERA RONDA", ["THE FINAL"] = "LA FINAL", ["MUSIC PLAYER"] = "REPRODUCTOR",
            ["MENU MUSIC"] = "MÚSICA DEL MENÚ", ["MATCH MUSIC"] = "MÚSICA DEL PARTIDO", ["SHUFFLE"] = "ALEATORIO",
            ["NOW PLAYING"] = "SONANDO", ["STOPPED"] = "DETENIDA", ["PLAYING"] = "SONANDO",
            ["SAVE COURT"] = "GUARDAR CANCHA", ["PLAY HERE"] = "JUGAR AQUÍ", ["DELETE COURT"] = "BORRAR CANCHA",
            ["FLOOR"] = "PISO", ["FLOOR COLOUR"] = "COLOR DEL PISO", ["LINES"] = "LÍNEAS",
            ["PAINT"] = "ZONA", ["BEHIND"] = "FONDO", ["CROWD"] = "PÚBLICO",
            ["CENTRE LOGO"] = "LOGO CENTRAL", ["LOGO COLOUR"] = "COLOR DEL LOGO", ["SAVED"] = "GUARDADA",
        };

        /// <summary>Phrases translated inside longer composite lines (longest first).</summary>
        private static readonly List<KeyValuePair<string, string>> Phrases = BuildPhrases();

        private static List<KeyValuePair<string, string>> BuildPhrases()
        {
            string[] keys =
            {
                "PLAYER OF THE GAME", "NEW RECORD", "DAILY COMPLETE!", "NEON STATIC BEATEN!", "RIVAL BEATEN!", "TITLES WON", "STANDINGS",
                "STARTING OVR", "PUT IN SPOT", "STEP ", "NICE!", "DAILY:", "SLAM!", "SWISH", "BADGE:", "CHAMPION:", "NEXT:",
                "EDITION", "SEASON ", "WEEK ", "STREAK", "BADGES", "DIFFICULTY", "SPOT ", "SEMIFINAL", "FINAL", "BEST",
                "SECRET CODE HINT FOUND!", "SECRET CODES", "Continues left:", "Next: stage", "Reached stage", "Try the stage again.",
                "CALLER CUP CHAMPIONS!", "Knocked out. Champion:", "Beat a CPU shooter's 3-point score in 60 seconds. Wins:", "In progress", "Eight-team knockout. Titles:", "BEAT ",
            };
            var es = new Dictionary<string, string>
            {
                ["PLAYER OF THE GAME"] = "JUGADOR DEL PARTIDO", ["NEW RECORD"] = "NUEVO RÉCORD", ["DAILY COMPLETE!"] = "¡RETO CUMPLIDO!",
                ["NEON STATIC BEATEN!"] = "¡NEON STATIC VENCIDO!", ["RIVAL BEATEN!"] = "¡RIVAL VENCIDO!", ["TITLES WON"] = "TÍTULOS GANADOS", ["STANDINGS"] = "CLASIFICACIÓN",
                ["STARTING OVR"] = "VALORACIÓN INICIAL", ["PUT IN SPOT"] = "PONER EN PUESTO", ["STEP "] = "PASO ", ["NICE!"] = "¡BIEN!",
                ["DAILY:"] = "RETO:", ["SLAM!"] = "¡MATE!", ["SWISH"] = "¡LIMPIA!", ["BADGE:"] = "INSIGNIA:", ["CHAMPION:"] = "CAMPEÓN:",
                ["NEXT:"] = "SIGUIENTE:", ["EDITION"] = "EDICIÓN", ["SEASON "] = "TEMPORADA ", ["WEEK "] = "SEMANA ", ["STREAK"] = "RACHA",
                ["BADGES"] = "INSIGNIAS", ["DIFFICULTY"] = "DIFICULTAD", ["SPOT "] = "PUESTO ", ["SEMIFINAL"] = "SEMIFINAL",
                ["FINAL"] = "FINAL", ["BEST"] = "MEJOR",
                ["SECRET CODE HINT FOUND!"] = "¡PISTA DE CÓDIGO SECRETO!", ["SECRET CODES"] = "CÓDIGOS SECRETOS",
                ["Continues left:"] = "Continues restantes:", ["Next: stage"] = "Siguiente: fase", ["Reached stage"] = "Llegaste a la fase",
                ["Try the stage again."] = "Repite la fase.",
                ["CALLER CUP CHAMPIONS!"] = "¡CAMPEONES DE LA CALLER CUP!", ["Knocked out. Champion:"] = "Eliminados. Campeón:",
                ["Beat a CPU shooter's 3-point score in 60 seconds. Wins:"] = "Supera los triples de un tirador de la CPU en 60 segundos. Victorias:",
                ["In progress"] = "En curso", ["Eight-team knockout. Titles:"] = "Eliminatoria de ocho equipos. Títulos:", ["BEAT "] = "SUPERA A ",
            };
            var list = new List<KeyValuePair<string, string>>();
            foreach (var k in keys) list.Add(new KeyValuePair<string, string>(k, es[k]));
            list.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
            return list;
        }
    }
}
