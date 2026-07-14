# Historial de cambios

Este proyecto utiliza versionado semántico.

## [Sin publicar]

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
