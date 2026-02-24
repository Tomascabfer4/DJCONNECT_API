# 📖 Endpoints de la API - DJ CONNECT

Resumen rápido de todas las funciones (endpoints) disponibles en la API de DJ CONNECT, organizadas por Controlador:

### 1. 🔐 UsuariosController (Identidad y Auth)
* **`POST /api/Usuarios/registro/cliente`**: Registra una nueva cuenta con el rol de "Cliente" y encripta su contraseña.
* **`POST /api/Usuarios/registro/dj`**: Registra una cuenta con el rol de "DJ" y genera automáticamente su perfil profesional público vacío.
* **`POST /api/Usuarios/login`**: Valida las credenciales y devuelve un Token JWT con los datos de sesión del usuario.
* **`GET /api/Usuarios/me`**: Valida el Token actual y devuelve los datos del usuario logueado para mantener la sesión activa en el frontend.
* **`PUT /api/Usuarios/perfil`**: Actualiza los datos personales básicos del usuario (nombre, teléfono, ubicación).
* **`PUT /api/Usuarios/perfil/foto`**: Sube una imagen física a Cloudinary, genera una URL segura y la guarda como foto de avatar.
* **`GET /api/Usuarios/{id}`** *(Futuro)*: Devuelve la información básica de cualquier usuario mediante su ID.
* **`DELETE /api/Usuarios/perfil/foto`** *(Futuro)*: Elimina la foto de perfil actual tanto de Cloudinary como de la base de datos.
* **`DELETE /api/Usuarios/desactivar-cuenta`** *(Futuro)*: Aplica un borrado lógico (Soft Delete) para ocultar la cuenta sin destruir el historial.

### 2. 🎧 DJsController (Buscador y Catálogo)
* **`GET /api/DJs`**: Devuelve el listado completo de todos los DJs activos de la plataforma.
* **`GET /api/DJs/buscar`**: Motor dinámico que filtra la lista de DJs según parámetros de consulta (nombre, género, ciudad, precio máximo).
* **`GET /api/DJs/{id}`**: Devuelve la ficha completa y detallada de un DJ (biografía, años de experiencia, nota media, etc.).
* **`PUT /api/DJs/perfil`**: Permite al DJ actualizar su carta de presentación profesional (tarifa, géneros musicales, biografía).

### 3. 📅 ReservasController (Contratos y Eventos)
* **`POST /api/Reservas`**: Crea una nueva solicitud de reserva, comprobando disponibilidad y calculando el presupuesto automáticamente.
* **`GET /api/Reservas`**: Devuelve el listado de reservas vinculadas al usuario que hace la petición (bandeja de entrada).
* **`PUT /api/Reservas/{id}/estado`**: Permite al DJ aceptar o rechazar un evento pendiente.
* **`PUT /api/Reservas/{id}`** *(Futuro)*: Permite al cliente editar los detalles de ubicación u horario de una solicitud que aún está pendiente.
* **`DELETE /api/Reservas/{id}`** *(Futuro)*: Marca una reserva como "cancelada" sin borrar la fila real de la base de datos.

### 4. 💬 ChatController (Mensajería WebSockets)
* **`GET /api/Chat/{reservaId}`**: Recupera todo el historial de mensajes pasados de una reserva concreta, ordenados por fecha.
* **`POST /api/Chat`**: Guarda un nuevo mensaje en la base de datos y lo emite en tiempo real a la otra persona mediante SignalR.

### 5. ⭐ ValoracionesController (Reseñas)
* **`POST /api/Valoraciones`**: Guarda una reseña (1-5 estrellas) verificando que el evento haya finalizado, y recalcula matemáticamente la nota media del DJ.
* **`GET /api/Valoraciones/dj/{djId}`**: Obtiene el listado completo de comentarios y estrellas que otros usuarios han dejado en el perfil de un DJ específico.

### 6. 🖼️ PortfolioController (Multimedia)
* **`POST /api/Portfolio/upload`**: Sube fotos, vídeos o pistas de audio a Cloudinary y añade el recurso a la galería del DJ.
* **`GET /api/Portfolio/{djId}`**: Recupera la galería multimedia completa de un DJ para mostrarla en su perfil.
* **`DELETE /api/Portfolio/{id}`**: Elimina permanentemente un archivo multimedia del servidor de Cloudinary y de la base de datos.

### 7. 📊 StatsController (Métricas Privadas)
* **`GET /api/Stats/dashboard`**: Realiza múltiples cálculos internos (ingresos, eventos pendientes, nota media, próximo bolo) y los devuelve en un único objeto JSON para el panel de control del DJ.
