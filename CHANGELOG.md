# Historial de cambios

Este proyecto utiliza versionado semántico.

## [Sin publicar]

## [1.10.0] - 2026-07-14

- Consulta explícita del historial de portapapeles de Windows al escribir `portapapeles` o `clipboard` en Salto, con filtro opcional y máximo de cinco textos.
- Resultados efímeros que se liberan al cambiar de consulta, ocultar Salto o cerrar la ventana; sin listener, historial propio, escritura en disco o telemetría.
- Estados claros para historial desactivado, acceso denegado, ausencia de resultados y carga superior a 120 ms, con cancelación de consultas obsoletas.
- Métricas DPI centralizadas para superficies dibujadas, relayout inmediato con `WM_DPICHANGED` y corrección no animada que respeta la preferencia de movimiento de Windows.
- Posición semántica de Mini por monitor, borde y altura relativa, con migración de coordenadas anteriores y alternativa visible cuando una pantalla se desconecta.
- Reanudación coherente de transiciones de Mini después de cambiar escala y finalización transaccional de transiciones de Salto para evitar geometría superpuesta.
- Pantalla de novedades actualizada para explicar el portapapeles bajo demanda, pantallas mixtas y el límite de privacidad local.
- Cobertura ampliada para monitores con coordenadas negativas, cambios de resolución, desconexión, DPI al 150%, cancelación asíncrona, historial desactivado y consumo en reposo.

## [1.9.1] - 2026-07-14

- Actualización más robusta cuando Raudo termina justo mientras Windows verifica el proceso que solicitó el reemplazo.
- Validación cerrada ante fallos de acceso: una ruta que no puede comprobarse mientras el proceso sigue activo continúa bloqueando la actualización.
- Cobertura para procesos ya terminados y para el rechazo de procesos activos cuya ruta no coincide con la instalación.

## [1.9.0] - 2026-07-14

- Bienvenida visual para la primera ejecución y resumen de novedades después de actualizar, disponible nuevamente desde el menú de bandeja.
- Recorrido animado y navegable por teclado que respeta tema, alto contraste, escala y preferencia de reducción de movimiento de Windows.
- Instalación directa desde el mismo ejecutable en el perfil del usuario, sin elevación, servicios o procesos auxiliares.
- Copia atómica con verificación SHA-256 y acceso nativo en el menú Inicio; el uso portable permanece disponible.
- Restauración exacta del cursor después de cada pulso, incluso en posiciones que el movimiento absoluto de Windows redondea entre píxeles.
- Interfaz de automatización limitada a instalación, apertura y acceso opcional en el escritorio, sin aceptar rutas ni comandos arbitrarios.
- Ejecutable estable y ejecutable versionado publicados junto al ZIP, sus sumas SHA-256 y certificaciones de procedencia.
- Validación ampliada para persistencia por versión, parámetros seguros, copia atómica, accesos de Windows, temas, escala y consumo de recursos.

## [1.8.4] - 2026-07-14

- Órdenes de voz locales en español mediante `Ctrl + Alt + V`, activadas únicamente bajo demanda y limitadas a una gramática cerrada de acciones seguras.
- Apertura de aplicaciones instaladas con alias localizados, controles de Pulso y multimedia, navegación y creación de escritorios, Recortes, cálculos y conversiones mediante voz.
- Segundo intento automático cuando una orden no se entiende o no coincide con una acción disponible, sin mantener el micrófono activo en segundo plano.
- Salto, la escucha de voz y sus confirmaciones siguen el escritorio activo en configuraciones con varios escritorios virtuales.
- Creación de escritorios desde la ventana principal, la bandeja o voz, acompañada de una guía introductoria disponible bajo demanda.
- Pulso conserva el vencimiento original y vuelve a establecer su solicitud a Windows después de una actualización o reinicio inesperado de Raudo.
- Las salidas explícitas, el bloqueo, la suspensión y el cierre de sesión continúan apagando Pulso y eliminando su estado persistido.
- Validación ampliada para gramática local, alias, reintentos, seguridad de comandos, seguimiento de escritorios, recuperación de Pulse y consumo de recursos.

## [1.7.0] - 2026-07-14

- Salto adapta su tamaño a la intención actual: una fila para cálculos, conversiones y búsquedas específicas, hasta cinco filas para búsquedas amplias.
- Las entradas numéricas y operaciones incompletas conservan una isla de cálculo compacta mientras se escribe.
- Transición de tamaño breve, interrumpible y sin rebote que mantiene estable la barra de búsqueda y respeta la reducción de movimiento de Windows.
- Zona de agarre para mover Salto, doble clic para centrar y posición recordada de forma segura por pantalla.
- Control de opacidad con niveles de 100%, 82% y 64%, acceso mediante `Ctrl + Shift + O` y opacidad completa obligatoria en alto contraste.
- Indicador interno de 3 píxeles para recorrer resultados adicionales sin una barra de desplazamiento convencional.
- Estado de preparación limitado a la carga real del catálogo de aplicaciones, con animación de baja frecuencia que se detiene al completar, ocultar o reducir movimiento.
- Validación ampliada para estados adaptativos, transiciones interrumpidas, carga, persistencia, temas, alto contraste, escalado al 150% y consumo de recursos.

## [1.6.0] - 2026-07-13

- Modo Mini incorpora controles directos de pista anterior, reproducción o pausa y pista siguiente sin abrir Salto.
- Los controles de escritorios permanecen en los extremos y utilizan iconos diferenciados de los controles de pista.
- Selector bajo demanda para dirigir la reproducción a las sesiones multimedia que Windows expone, con retorno seguro al control automático.
- Mini omite acciones de pista que el reproductor seleccionado declara como no disponibles.
- Menú secundario unificado para elegir reproductor, ajustar el volumen global, traer ventanas y abrir opciones de Raudo.
- Consulta local limitada al nombre de la aplicación y estado de reproducción; sin títulos, contenido, direcciones, carátulas, historial o sondeo periódico.
- Contratos oficiales de Windows restaurados únicamente durante la compilación con versión y suma SHA-256 fijadas, sin nuevas DLL en el paquete.
- Validación ampliada para temas, alto contraste, escalado al 150%, selección de sesiones y consumo en reposo.

## [1.5.0] - 2026-07-13

- Seis controles multimedia fijos para reproducir o pausar, cambiar de pista, silenciar y ajustar el volumen de Windows.
- Acceso desde el submenú Multimedia de la bandeja y mediante búsquedas en Salto, sin ampliar su vista inicial.
- Comandos explícitos mediante las teclas multimedia documentadas de Windows, sin interpretar la consulta como una entrada arbitraria.
- Iconos vectoriales diferenciados y ayuda contextual para las acciones multimedia.
- Sin lectura de sesiones, reproductores, títulos, contenido o carátulas; tampoco se agregan sondeo, historial, temporizadores ni conexiones de red.
- Validación ampliada para el mapeo de comandos, temas, alto contraste, escalado y consumo de recursos.

## [1.4.0] - 2026-07-13

- Salto calcula expresiones aritméticas acotadas y convierte unidades comunes de longitud, masa, temperatura, tiempo y almacenamiento sin conexión.
- Los resultados de cálculo y conversión se copian únicamente después de seleccionarlos con `Enter` o un clic.
- Acceso directo a carpetas conocidas y existentes de Windows, limitado a ubicaciones locales no remotas.
- Resultados generados por consulta sin volver a enumerar ventanas ni aplicaciones en cada pulsación.
- Nuevos iconos vectoriales y ayuda de teclado contextual para copiar, abrir, traer o ejecutar.
- Validación ampliada para entradas inválidas, rutas remotas, temas, alto contraste, escalado y consumo de recursos.

## [1.3.0] - 2026-07-13

- Salto ahora encuentra ventanas abiertas por título o aplicación y distingue el escritorio actual de otro escritorio.
- Una ventana existente tiene prioridad sobre iniciar otra instancia y puede enfocarse o traerse al escritorio actual.
- Búsqueda de aplicaciones instaladas mediante el catálogo de aplicaciones de Windows, cargado una vez y bajo demanda.
- Vista inicial limitada a las acciones de Raudo y máximo acotado de resultados durante una búsqueda.
- Nuevos estados de preparación, ausencia de resultados y error de ejecución sin historial de consultas.
- Validación ampliada para aplicaciones, ventanas actuales y externas, escalado y consumo de recursos.

## [1.2.0] - 2026-07-13

- Nuevo lanzador Salto para buscar y ejecutar acciones locales con `Ctrl + Alt + Espacio`.
- Catálogo limitado a funciones internas de Raudo, sin scripts, historial, indexación ni conexiones adicionales.
- Acciones para Pulso, Recortes, la ventana principal, Modo Mini y escritorios virtuales disponibles.
- Navegación completa por teclado, búsqueda sin distinción de mayúsculas o acentos y acceso alternativo desde la bandeja.
- Rediseño compacto de la ventana principal con componentes reutilizables y estados visuales más discretos.
- Compatibilidad explícita con alto contraste, escalado de pantalla y reducción de movimiento.

## [1.1.0] - 2026-07-13

- Nuevo nombre Pulso para la función de disponibilidad temporal y sus recordatorios.
- Actualización confirmada desde la aplicación con validación de versión y sumas SHA-256.
- Transición conectada de la ventana principal hacia Modo Mini al minimizar.
- Indicadores independientes para sesión activa, últimos 15 minutos, últimos 5 minutos y finalización.
- Recordatorios progresivos que respetan pantalla completa, presentaciones y el estado de notificaciones de Windows.
- Opacidad adaptativa de Mini con transiciones breves y sin trabajo continuo de animación.
- Rediseño del estado recogido, la marca y las transiciones de Modo Mini.

## [1.0.1] - 2026-07-13

- Seguimiento automático de Raudo Mini en el escritorio activo.
- Navegación que oculta las direcciones sin escritorio adyacente.
- Estado recogido como una pestaña discreta en el borde de la pantalla.
- Validación entre procesos de la visibilidad en todos los escritorios.

## [1.0.0] - 2026-07-13

- Modo Mini con navegación visual entre escritorios virtuales.
- Selector bajo demanda para traer ventanas al escritorio actual.
- Posición persistente, soporte multimonitor y degradación segura por versión de Windows.
- Prueba de integración cruzada para ventanas pertenecientes a otro proceso.
- Nueva identidad visual y nombre Raudo.
- Interfaz compatible con temas claro y oscuro.
- Mantener activo con duración limitada y temporizador dinámico.
- Apagado automático al bloquear, suspender o cerrar la sesión interactiva.
- Acceso a la Herramienta Recortes de Windows.
- Consulta manual y segura de publicaciones en GitHub.
- Build, pruebas, empaquetado portable y atestación de procedencia.
