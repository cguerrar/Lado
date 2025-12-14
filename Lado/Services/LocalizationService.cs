using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Lado.Models;

namespace Lado.Services
{
    public interface ILocalizationService
    {
        string Get(string key);
        string Get(string key, params object[] args);
        string GetForCulture(string key, string culture);
        IReadOnlyDictionary<string, string> GetAllTranslations();
    }

    public class LocalizationService : ILocalizationService
    {
        private readonly ConcurrentDictionary<string, Dictionary<string, string>> _translations;

        public LocalizationService()
        {
            _translations = new ConcurrentDictionary<string, Dictionary<string, string>>();
            LoadTranslations();
        }

        private void LoadTranslations()
        {
            // ========================================
            // ESPAÑOL (es) - Idioma por defecto
            // ========================================
            _translations["es"] = new Dictionary<string, string>
            {
                // === NAVEGACIÓN ===
                ["Nav_Feed"] = "Feed",
                ["Nav_Create"] = "Crear",
                ["Nav_Search"] = "Buscar...",
                ["Nav_Notifications"] = "Notificaciones",
                ["Nav_Messages"] = "Mensajes",
                ["Nav_Profile"] = "Perfil",
                ["Nav_Settings"] = "Configuracion",
                ["Nav_Dashboard"] = "Dashboard",
                ["Nav_Wallet"] = "Billetera",
                ["Nav_Logout"] = "Cerrar sesion",
                ["Nav_Login"] = "Entrar",
                ["Nav_Register"] = "Crear cuenta",
                ["Nav_Explore"] = "Explorar",
                ["Nav_Home"] = "Inicio",
                ["Nav_Admin"] = "Admin",
                ["Nav_Challenges"] = "Desafios",
                ["Nav_Favorites"] = "Favoritos",
                ["Nav_Subscriptions"] = "Suscripciones",
                ["Nav_Statistics"] = "Estadisticas",
                ["Nav_Activity"] = "Actividad",
                ["Nav_MyContent"] = "Mi Contenido",
                ["Nav_MyAgency"] = "Mi Agencia",
                ["Nav_Help"] = "Centro de Ayuda",

                // === AUTENTICACIÓN ===
                ["Auth_Login"] = "Iniciar sesion",
                ["Auth_Register"] = "Registrarse",
                ["Auth_Email"] = "Correo electronico",
                ["Auth_Password"] = "Contrasena",
                ["Auth_ConfirmPassword"] = "Confirmar contrasena",
                ["Auth_FullName"] = "Nombre completo",
                ["Auth_Username"] = "Nombre de usuario",
                ["Auth_RememberMe"] = "Recordarme",
                ["Auth_ForgotPassword"] = "Olvide mi contrasena?",
                ["Auth_NoAccount"] = "No tienes cuenta?",
                ["Auth_HaveAccount"] = "Ya tienes cuenta?",
                ["Auth_LoginWithGoogle"] = "Continuar con Google",
                ["Auth_Or"] = "o",
                ["Auth_Welcome"] = "Bienvenido",
                ["Auth_WelcomeBack"] = "Bienvenido de vuelta",
                ["Auth_CreateAccount"] = "Crear tu cuenta",
                ["Auth_LoginSubtitle"] = "Ingresa tus credenciales para continuar",
                ["Auth_RegisterSubtitle"] = "Completa tus datos para comenzar",
                ["Auth_PasswordRequirements"] = "Minimo 8 caracteres, incluir numeros",
                ["Auth_AcceptTerms"] = "Acepto los terminos y condiciones",
                ["Auth_Over18"] = "Confirmo que tengo 18 anos o mas",
                ["Auth_EmailOrUsername"] = "Email o usuario",
                ["Auth_EmailPlaceholder"] = "tu@email.com",
                ["Auth_OrContinueWith"] = "o continua con",

                // === LOGIN PAGE ===
                ["Login_VisualTitle"] = "Tu otro lado te espera",
                ["Login_VisualSubtitle"] = "El espacio donde no tienes que fingir. Comparte lo que realmente eres, sin filtros ni juicios.",
                ["Login_FeaturePrivate"] = "Privado",
                ["Login_FeatureNoCensorship"] = "Sin censura",
                ["Login_FeatureMonetize"] = "Monetiza",
                ["Login_Beta"] = "En Beta",

                // === REGISTER PAGE ===
                ["Register_DualIdentity"] = "Doble identidad creativa",
                ["Register_DualIdentityDesc"] = "Publica contenido gratuito con tu nombre real y contenido premium con tu seudonimo.",
                ["Register_AccountDetails"] = "Datos de la cuenta",
                ["Register_UsernamePlaceholder"] = "usuario123",
                ["Register_RealIdentity"] = "tu identidad real",
                ["Register_FullNamePlaceholder"] = "Juan Perez",
                ["Register_Pseudonym"] = "Seudonimo",
                ["Register_ForPremiumContent"] = "para contenido premium",
                ["Register_PseudonymPlaceholder"] = "ElMisterioso",
                ["Register_PwdReq8Chars"] = "Extension minima de 8 caracteres",
                ["Register_PwdReqNumber"] = "Debe contener al menos 1 numero o simbolo",
                ["Register_PwdReqLetter"] = "Debe contener al menos 1 letra",
                ["Register_AcceptTermsText"] = "Al crear una cuenta, aceptas la",
                ["Register_PrivacyPolicy"] = "Politica de privacidad",
                ["Register_RegistrationAgreement"] = "Acuerdo de registro",
                ["Register_CreateMyAccount"] = "Crear Mi Cuenta",

                // === FEED ===
                ["Feed_Title"] = "Feed",
                ["Feed_EmptyState"] = "No hay publicaciones para mostrar",
                ["Feed_EmptyStateSubtitle"] = "Sigue a creadores para ver su contenido aqui",
                ["Feed_Like"] = "Me gusta",
                ["Feed_Comment"] = "Comentar",
                ["Feed_Share"] = "Compartir",
                ["Feed_Save"] = "Guardar",
                ["Feed_Follow"] = "Seguir",
                ["Feed_Following"] = "Siguiendo",
                ["Feed_Unfollow"] = "Dejar de seguir",
                ["Feed_Subscribe"] = "Suscribirse",
                ["Feed_Subscribed"] = "Suscrito",
                ["Feed_ViewPost"] = "Ver publicacion",
                ["Feed_CopyLink"] = "Copiar enlace",
                ["Feed_Report"] = "Reportar",
                ["Feed_Block"] = "Bloquear usuario",
                ["Feed_AddToFavorites"] = "Agregar a favoritos",
                ["Feed_Premium"] = "Contenido Premium",
                ["Feed_Unlock"] = "Desbloquear",
                ["Feed_SubscribeToSee"] = "Suscribete para ver este contenido",
                ["Feed_SuggestionsForYou"] = "Sugerencias para ti",
                ["Feed_PremiumCreators"] = "Creadores Premium",
                ["Feed_Trending"] = "Tendencias",
                ["Feed_WriteComment"] = "Escribe un comentario...",
                ["Feed_Send"] = "Enviar",
                ["Feed_ViewAllComments"] = "Ver todos los comentarios",
                ["Feed_Stories"] = "Historias",
                ["Feed_Collections"] = "Colecciones",
                ["Feed_LadoA"] = "Lado A",
                ["Feed_LadoB"] = "Lado B",

                // === CONTENIDO ===
                ["Content_Create"] = "Crear contenido",
                ["Content_Upload"] = "Subir",
                ["Content_Description"] = "Descripcion",
                ["Content_DescriptionPlaceholder"] = "Escribe algo sobre tu contenido...",
                ["Content_SelectFile"] = "Seleccionar archivo",
                ["Content_DragDrop"] = "Arrastra y suelta tu archivo aqui",
                ["Content_Photo"] = "Foto",
                ["Content_Video"] = "Video",
                ["Content_Audio"] = "Audio",
                ["Content_Text"] = "Texto",
                ["Content_Price"] = "Precio",
                ["Content_Free"] = "Gratis",
                ["Content_Premium"] = "Premium",
                ["Content_Public"] = "Publico",
                ["Content_Private"] = "Privado",
                ["Content_Publish"] = "Publicar",
                ["Content_SaveDraft"] = "Guardar borrador",
                ["Content_Schedule"] = "Programar",
                ["Content_Delete"] = "Eliminar",
                ["Content_Edit"] = "Editar",
                ["Content_Preview"] = "Vista previa",

                // === PERFIL ===
                ["Profile_Title"] = "Perfil",
                ["Profile_MyProfile"] = "Mi Perfil",
                ["Profile_EditProfile"] = "Editar perfil",
                ["Profile_Posts"] = "Publicaciones",
                ["Profile_Followers"] = "Seguidores",
                ["Profile_Following"] = "Siguiendo",
                ["Profile_Bio"] = "Biografia",
                ["Profile_Verified"] = "Verificado",
                ["Profile_Creator"] = "Creador",
                ["Profile_Fan"] = "Fan",
                ["Profile_JoinedOn"] = "Se unio en",
                ["Profile_Location"] = "Ubicacion",
                ["Profile_Website"] = "Sitio web",
                ["Profile_SubscriptionPrice"] = "Precio de suscripcion",
                ["Profile_PerMonth"] = "/mes",

                // === CONFIGURACIÓN ===
                ["Settings_Title"] = "Configuracion",
                ["Settings_Account"] = "Cuenta",
                ["Settings_Security"] = "Seguridad",
                ["Settings_Notifications"] = "Notificaciones",
                ["Settings_Privacy"] = "Privacidad",
                ["Settings_Language"] = "Idioma",
                ["Settings_Theme"] = "Tema",
                ["Settings_DarkMode"] = "Modo oscuro",
                ["Settings_LightMode"] = "Modo claro",
                ["Settings_SystemTheme"] = "Tema del sistema",
                ["Settings_Save"] = "Guardar cambios",
                ["Settings_Cancel"] = "Cancelar",
                ["Settings_ChangePassword"] = "Cambiar contrasena",
                ["Settings_CurrentPassword"] = "Contrasena actual",
                ["Settings_NewPassword"] = "Nueva contrasena",
                ["Settings_ConfirmNewPassword"] = "Confirmar nueva contrasena",
                ["Settings_DeleteAccount"] = "Eliminar cuenta",
                ["Settings_DeleteAccountWarning"] = "Esta accion es irreversible",
                ["Settings_BlockedUsers"] = "Usuarios bloqueados",
                ["Settings_BlockedUsersDesc"] = "Gestiona la lista de usuarios que has bloqueado",
                ["Settings_ViewBlocked"] = "Ver bloqueados",
                ["Settings_SelectLanguage"] = "Selecciona tu idioma preferido",
                ["Settings_LanguageSpanish"] = "Espanol (Latino)",
                ["Settings_LanguageEnglish"] = "English",
                ["Settings_LanguagePortuguese"] = "Portugues",

                // === MENSAJES ===
                ["Messages_Title"] = "Mensajes",
                ["Messages_NewMessage"] = "Nuevo mensaje",
                ["Messages_Search"] = "Buscar conversacion...",
                ["Messages_NoMessages"] = "No tienes mensajes",
                ["Messages_TypeMessage"] = "Escribe un mensaje...",
                ["Messages_Send"] = "Enviar",
                ["Messages_Online"] = "En linea",
                ["Messages_Offline"] = "Desconectado",
                ["Messages_LastSeen"] = "Ultima vez",
                ["Messages_Unread"] = "Sin leer",

                // === BILLETERA ===
                ["Wallet_Title"] = "Billetera",
                ["Wallet_Balance"] = "Saldo disponible",
                ["Wallet_Earnings"] = "Ganancias",
                ["Wallet_ThisMonth"] = "Este mes",
                ["Wallet_Withdraw"] = "Retirar",
                ["Wallet_AddFunds"] = "Agregar fondos",
                ["Wallet_Transactions"] = "Transacciones",
                ["Wallet_NoTransactions"] = "No hay transacciones",
                ["Wallet_Pending"] = "Pendiente",
                ["Wallet_Completed"] = "Completado",
                ["Wallet_Failed"] = "Fallido",

                // === DASHBOARD ===
                ["Dashboard_Title"] = "Dashboard",
                ["Dashboard_Overview"] = "Resumen",
                ["Dashboard_Subscribers"] = "Suscriptores",
                ["Dashboard_Revenue"] = "Ingresos",
                ["Dashboard_Views"] = "Vistas",
                ["Dashboard_Engagement"] = "Engagement",
                ["Dashboard_Growth"] = "Crecimiento",
                ["Dashboard_Analytics"] = "Analiticas",

                // === SUSCRIPCIONES ===
                ["Subscription_Subscribe"] = "Suscribirse",
                ["Subscription_Unsubscribe"] = "Cancelar suscripcion",
                ["Subscription_Renew"] = "Renovar",
                ["Subscription_Expires"] = "Expira el",
                ["Subscription_Active"] = "Activa",
                ["Subscription_Expired"] = "Expirada",
                ["Subscription_Cancelled"] = "Cancelada",
                ["Subscription_Monthly"] = "Mensual",
                ["Subscription_Yearly"] = "Anual",
                ["Subscription_Benefits"] = "Beneficios",
                ["Subscription_IncludesLadoB"] = "Incluye contenido Lado B",

                // === DESAFÍOS ===
                ["Challenge_Title"] = "Desafios",
                ["Challenge_Create"] = "Crear desafio",
                ["Challenge_Budget"] = "Presupuesto",
                ["Challenge_Deadline"] = "Fecha limite",
                ["Challenge_Participants"] = "Participantes",
                ["Challenge_Submit"] = "Enviar propuesta",
                ["Challenge_Pending"] = "Pendiente",
                ["Challenge_Accepted"] = "Aceptado",
                ["Challenge_Completed"] = "Completado",
                ["Challenge_Rejected"] = "Rechazado",
                ["Challenge_PublicFeed"] = "Feed Publico",
                ["Challenge_MyChallenges"] = "Mis Desafios",
                ["Challenge_Received"] = "Desafios Recibidos",
                ["Challenge_MyProposals"] = "Mis Propuestas",

                // === AGENCIA ===
                ["Agency_MyAds"] = "Mis Anuncios",
                ["Agency_CreateAd"] = "Crear Anuncio",
                ["Agency_Recharge"] = "Recargar Saldo",

                // === BOTONES COMUNES ===
                ["Button_Save"] = "Guardar",
                ["Button_Cancel"] = "Cancelar",
                ["Button_Delete"] = "Eliminar",
                ["Button_Edit"] = "Editar",
                ["Button_Submit"] = "Enviar",
                ["Button_Close"] = "Cerrar",
                ["Button_Confirm"] = "Confirmar",
                ["Button_Back"] = "Volver",
                ["Button_Next"] = "Siguiente",
                ["Button_Previous"] = "Anterior",
                ["Button_See_More"] = "Ver mas",
                ["Button_See_Less"] = "Ver menos",
                ["Button_Loading"] = "Cargando...",
                ["Button_Retry"] = "Reintentar",
                ["Button_ViewAll"] = "Ver todo",

                // === MENSAJES DEL SISTEMA ===
                ["Msg_Success"] = "Operacion exitosa",
                ["Msg_Error"] = "Ocurrio un error",
                ["Msg_Saved"] = "Guardado correctamente",
                ["Msg_Deleted"] = "Eliminado correctamente",
                ["Msg_Updated"] = "Actualizado correctamente",
                ["Msg_Copied"] = "Copiado al portapapeles",
                ["Msg_Loading"] = "Cargando...",
                ["Msg_NoResults"] = "No se encontraron resultados",
                ["Msg_ConfirmDelete"] = "Estas seguro de eliminar?",
                ["Msg_ConfirmAction"] = "Estas seguro?",
                ["Msg_SessionExpired"] = "Tu sesion ha expirado",
                ["Msg_NetworkError"] = "Error de conexion",
                ["Msg_InvalidCredentials"] = "Credenciales invalidas",
                ["Msg_RequiredField"] = "Este campo es obligatorio",
                ["Msg_InvalidEmail"] = "Correo electronico invalido",
                ["Msg_PasswordMismatch"] = "Las contrasenas no coinciden",
                ["Msg_UserBlocked"] = "Has bloqueado a este usuario",
                ["Msg_CantSendMessage"] = "No puedes enviar mensajes a este usuario",
                ["Msg_LanguageChanged"] = "Idioma cambiado correctamente",

                // === TIEMPO ===
                ["Time_Now"] = "Ahora",
                ["Time_MinutesAgo"] = "hace {0} min",
                ["Time_HoursAgo"] = "hace {0} h",
                ["Time_DaysAgo"] = "hace {0} d",
                ["Time_Yesterday"] = "Ayer",
                ["Time_Today"] = "Hoy",

                // === FOOTER ===
                ["Footer_Terms"] = "Terminos",
                ["Footer_Privacy"] = "Privacidad",
                ["Footer_Help"] = "Ayuda",
                ["Footer_About"] = "Acerca de",
                ["Footer_Contact"] = "Contacto",
                ["Footer_Copyright"] = "Todos los derechos reservados",
            };

            // ========================================
            // ENGLISH (en)
            // ========================================
            _translations["en"] = new Dictionary<string, string>
            {
                // === NAVIGATION ===
                ["Nav_Feed"] = "Feed",
                ["Nav_Create"] = "Create",
                ["Nav_Search"] = "Search...",
                ["Nav_Notifications"] = "Notifications",
                ["Nav_Messages"] = "Messages",
                ["Nav_Profile"] = "Profile",
                ["Nav_Settings"] = "Settings",
                ["Nav_Dashboard"] = "Dashboard",
                ["Nav_Wallet"] = "Wallet",
                ["Nav_Logout"] = "Log out",
                ["Nav_Login"] = "Log in",
                ["Nav_Register"] = "Sign up",
                ["Nav_Explore"] = "Explore",
                ["Nav_Home"] = "Home",
                ["Nav_Admin"] = "Admin",
                ["Nav_Challenges"] = "Challenges",
                ["Nav_Favorites"] = "Favorites",
                ["Nav_Subscriptions"] = "Subscriptions",
                ["Nav_Statistics"] = "Statistics",
                ["Nav_Activity"] = "Activity",
                ["Nav_MyContent"] = "My Content",
                ["Nav_MyAgency"] = "My Agency",
                ["Nav_Help"] = "Help Center",

                // === AUTHENTICATION ===
                ["Auth_Login"] = "Log in",
                ["Auth_Register"] = "Sign up",
                ["Auth_Email"] = "Email",
                ["Auth_Password"] = "Password",
                ["Auth_ConfirmPassword"] = "Confirm password",
                ["Auth_FullName"] = "Full name",
                ["Auth_Username"] = "Username",
                ["Auth_RememberMe"] = "Remember me",
                ["Auth_ForgotPassword"] = "Forgot password?",
                ["Auth_NoAccount"] = "Don't have an account?",
                ["Auth_HaveAccount"] = "Already have an account?",
                ["Auth_LoginWithGoogle"] = "Continue with Google",
                ["Auth_Or"] = "or",
                ["Auth_Welcome"] = "Welcome",
                ["Auth_WelcomeBack"] = "Welcome back",
                ["Auth_CreateAccount"] = "Create your account",
                ["Auth_LoginSubtitle"] = "Enter your credentials to continue",
                ["Auth_RegisterSubtitle"] = "Fill in your details to get started",
                ["Auth_PasswordRequirements"] = "Minimum 8 characters, include numbers",
                ["Auth_AcceptTerms"] = "I accept the terms and conditions",
                ["Auth_Over18"] = "I confirm I am 18 years or older",
                ["Auth_EmailOrUsername"] = "Email or username",
                ["Auth_EmailPlaceholder"] = "you@email.com",
                ["Auth_OrContinueWith"] = "or continue with",

                // === LOGIN PAGE ===
                ["Login_VisualTitle"] = "Your other side awaits",
                ["Login_VisualSubtitle"] = "The space where you don't have to pretend. Share who you really are, without filters or judgments.",
                ["Login_FeaturePrivate"] = "Private",
                ["Login_FeatureNoCensorship"] = "No censorship",
                ["Login_FeatureMonetize"] = "Monetize",
                ["Login_Beta"] = "In Beta",

                // === REGISTER PAGE ===
                ["Register_DualIdentity"] = "Dual creative identity",
                ["Register_DualIdentityDesc"] = "Post free content with your real name and premium content with your pseudonym.",
                ["Register_AccountDetails"] = "Account details",
                ["Register_UsernamePlaceholder"] = "username123",
                ["Register_RealIdentity"] = "your real identity",
                ["Register_FullNamePlaceholder"] = "John Doe",
                ["Register_Pseudonym"] = "Pseudonym",
                ["Register_ForPremiumContent"] = "for premium content",
                ["Register_PseudonymPlaceholder"] = "TheMystery",
                ["Register_PwdReq8Chars"] = "Minimum 8 characters",
                ["Register_PwdReqNumber"] = "Must contain at least 1 number or symbol",
                ["Register_PwdReqLetter"] = "Must contain at least 1 letter",
                ["Register_AcceptTermsText"] = "By creating an account, you accept the",
                ["Register_PrivacyPolicy"] = "Privacy Policy",
                ["Register_RegistrationAgreement"] = "Registration Agreement",
                ["Register_CreateMyAccount"] = "Create My Account",

                // === FEED ===
                ["Feed_Title"] = "Feed",
                ["Feed_EmptyState"] = "No posts to show",
                ["Feed_EmptyStateSubtitle"] = "Follow creators to see their content here",
                ["Feed_Like"] = "Like",
                ["Feed_Comment"] = "Comment",
                ["Feed_Share"] = "Share",
                ["Feed_Save"] = "Save",
                ["Feed_Follow"] = "Follow",
                ["Feed_Following"] = "Following",
                ["Feed_Unfollow"] = "Unfollow",
                ["Feed_Subscribe"] = "Subscribe",
                ["Feed_Subscribed"] = "Subscribed",
                ["Feed_ViewPost"] = "View post",
                ["Feed_CopyLink"] = "Copy link",
                ["Feed_Report"] = "Report",
                ["Feed_Block"] = "Block user",
                ["Feed_AddToFavorites"] = "Add to favorites",
                ["Feed_Premium"] = "Premium Content",
                ["Feed_Unlock"] = "Unlock",
                ["Feed_SubscribeToSee"] = "Subscribe to see this content",
                ["Feed_SuggestionsForYou"] = "Suggestions for you",
                ["Feed_PremiumCreators"] = "Premium Creators",
                ["Feed_Trending"] = "Trending",
                ["Feed_WriteComment"] = "Write a comment...",
                ["Feed_Send"] = "Send",
                ["Feed_ViewAllComments"] = "View all comments",
                ["Feed_Stories"] = "Stories",
                ["Feed_Collections"] = "Collections",
                ["Feed_LadoA"] = "Side A",
                ["Feed_LadoB"] = "Side B",

                // === CONTENT ===
                ["Content_Create"] = "Create content",
                ["Content_Upload"] = "Upload",
                ["Content_Description"] = "Description",
                ["Content_DescriptionPlaceholder"] = "Write something about your content...",
                ["Content_SelectFile"] = "Select file",
                ["Content_DragDrop"] = "Drag and drop your file here",
                ["Content_Photo"] = "Photo",
                ["Content_Video"] = "Video",
                ["Content_Audio"] = "Audio",
                ["Content_Text"] = "Text",
                ["Content_Price"] = "Price",
                ["Content_Free"] = "Free",
                ["Content_Premium"] = "Premium",
                ["Content_Public"] = "Public",
                ["Content_Private"] = "Private",
                ["Content_Publish"] = "Publish",
                ["Content_SaveDraft"] = "Save draft",
                ["Content_Schedule"] = "Schedule",
                ["Content_Delete"] = "Delete",
                ["Content_Edit"] = "Edit",
                ["Content_Preview"] = "Preview",

                // === PROFILE ===
                ["Profile_Title"] = "Profile",
                ["Profile_MyProfile"] = "My Profile",
                ["Profile_EditProfile"] = "Edit profile",
                ["Profile_Posts"] = "Posts",
                ["Profile_Followers"] = "Followers",
                ["Profile_Following"] = "Following",
                ["Profile_Bio"] = "Bio",
                ["Profile_Verified"] = "Verified",
                ["Profile_Creator"] = "Creator",
                ["Profile_Fan"] = "Fan",
                ["Profile_JoinedOn"] = "Joined",
                ["Profile_Location"] = "Location",
                ["Profile_Website"] = "Website",
                ["Profile_SubscriptionPrice"] = "Subscription price",
                ["Profile_PerMonth"] = "/month",

                // === SETTINGS ===
                ["Settings_Title"] = "Settings",
                ["Settings_Account"] = "Account",
                ["Settings_Security"] = "Security",
                ["Settings_Notifications"] = "Notifications",
                ["Settings_Privacy"] = "Privacy",
                ["Settings_Language"] = "Language",
                ["Settings_Theme"] = "Theme",
                ["Settings_DarkMode"] = "Dark mode",
                ["Settings_LightMode"] = "Light mode",
                ["Settings_SystemTheme"] = "System theme",
                ["Settings_Save"] = "Save changes",
                ["Settings_Cancel"] = "Cancel",
                ["Settings_ChangePassword"] = "Change password",
                ["Settings_CurrentPassword"] = "Current password",
                ["Settings_NewPassword"] = "New password",
                ["Settings_ConfirmNewPassword"] = "Confirm new password",
                ["Settings_DeleteAccount"] = "Delete account",
                ["Settings_DeleteAccountWarning"] = "This action is irreversible",
                ["Settings_BlockedUsers"] = "Blocked users",
                ["Settings_BlockedUsersDesc"] = "Manage your blocked users list",
                ["Settings_ViewBlocked"] = "View blocked",
                ["Settings_SelectLanguage"] = "Select your preferred language",
                ["Settings_LanguageSpanish"] = "Spanish (Latin)",
                ["Settings_LanguageEnglish"] = "English",
                ["Settings_LanguagePortuguese"] = "Portuguese",

                // === MESSAGES ===
                ["Messages_Title"] = "Messages",
                ["Messages_NewMessage"] = "New message",
                ["Messages_Search"] = "Search conversation...",
                ["Messages_NoMessages"] = "You have no messages",
                ["Messages_TypeMessage"] = "Type a message...",
                ["Messages_Send"] = "Send",
                ["Messages_Online"] = "Online",
                ["Messages_Offline"] = "Offline",
                ["Messages_LastSeen"] = "Last seen",
                ["Messages_Unread"] = "Unread",

                // === WALLET ===
                ["Wallet_Title"] = "Wallet",
                ["Wallet_Balance"] = "Available balance",
                ["Wallet_Earnings"] = "Earnings",
                ["Wallet_ThisMonth"] = "This month",
                ["Wallet_Withdraw"] = "Withdraw",
                ["Wallet_AddFunds"] = "Add funds",
                ["Wallet_Transactions"] = "Transactions",
                ["Wallet_NoTransactions"] = "No transactions",
                ["Wallet_Pending"] = "Pending",
                ["Wallet_Completed"] = "Completed",
                ["Wallet_Failed"] = "Failed",

                // === DASHBOARD ===
                ["Dashboard_Title"] = "Dashboard",
                ["Dashboard_Overview"] = "Overview",
                ["Dashboard_Subscribers"] = "Subscribers",
                ["Dashboard_Revenue"] = "Revenue",
                ["Dashboard_Views"] = "Views",
                ["Dashboard_Engagement"] = "Engagement",
                ["Dashboard_Growth"] = "Growth",
                ["Dashboard_Analytics"] = "Analytics",

                // === SUBSCRIPTIONS ===
                ["Subscription_Subscribe"] = "Subscribe",
                ["Subscription_Unsubscribe"] = "Cancel subscription",
                ["Subscription_Renew"] = "Renew",
                ["Subscription_Expires"] = "Expires on",
                ["Subscription_Active"] = "Active",
                ["Subscription_Expired"] = "Expired",
                ["Subscription_Cancelled"] = "Cancelled",
                ["Subscription_Monthly"] = "Monthly",
                ["Subscription_Yearly"] = "Yearly",
                ["Subscription_Benefits"] = "Benefits",
                ["Subscription_IncludesLadoB"] = "Includes Side B content",

                // === CHALLENGES ===
                ["Challenge_Title"] = "Challenges",
                ["Challenge_Create"] = "Create challenge",
                ["Challenge_Budget"] = "Budget",
                ["Challenge_Deadline"] = "Deadline",
                ["Challenge_Participants"] = "Participants",
                ["Challenge_Submit"] = "Submit proposal",
                ["Challenge_Pending"] = "Pending",
                ["Challenge_Accepted"] = "Accepted",
                ["Challenge_Completed"] = "Completed",
                ["Challenge_Rejected"] = "Rejected",
                ["Challenge_PublicFeed"] = "Public Feed",
                ["Challenge_MyChallenges"] = "My Challenges",
                ["Challenge_Received"] = "Received Challenges",
                ["Challenge_MyProposals"] = "My Proposals",

                // === AGENCY ===
                ["Agency_MyAds"] = "My Ads",
                ["Agency_CreateAd"] = "Create Ad",
                ["Agency_Recharge"] = "Recharge Balance",

                // === COMMON BUTTONS ===
                ["Button_Save"] = "Save",
                ["Button_Cancel"] = "Cancel",
                ["Button_Delete"] = "Delete",
                ["Button_Edit"] = "Edit",
                ["Button_Submit"] = "Submit",
                ["Button_Close"] = "Close",
                ["Button_Confirm"] = "Confirm",
                ["Button_Back"] = "Back",
                ["Button_Next"] = "Next",
                ["Button_Previous"] = "Previous",
                ["Button_See_More"] = "See more",
                ["Button_See_Less"] = "See less",
                ["Button_Loading"] = "Loading...",
                ["Button_Retry"] = "Retry",
                ["Button_ViewAll"] = "View all",

                // === SYSTEM MESSAGES ===
                ["Msg_Success"] = "Operation successful",
                ["Msg_Error"] = "An error occurred",
                ["Msg_Saved"] = "Saved successfully",
                ["Msg_Deleted"] = "Deleted successfully",
                ["Msg_Updated"] = "Updated successfully",
                ["Msg_Copied"] = "Copied to clipboard",
                ["Msg_Loading"] = "Loading...",
                ["Msg_NoResults"] = "No results found",
                ["Msg_ConfirmDelete"] = "Are you sure you want to delete?",
                ["Msg_ConfirmAction"] = "Are you sure?",
                ["Msg_SessionExpired"] = "Your session has expired",
                ["Msg_NetworkError"] = "Connection error",
                ["Msg_InvalidCredentials"] = "Invalid credentials",
                ["Msg_RequiredField"] = "This field is required",
                ["Msg_InvalidEmail"] = "Invalid email",
                ["Msg_PasswordMismatch"] = "Passwords don't match",
                ["Msg_UserBlocked"] = "You have blocked this user",
                ["Msg_CantSendMessage"] = "You cannot send messages to this user",
                ["Msg_LanguageChanged"] = "Language changed successfully",

                // === TIME ===
                ["Time_Now"] = "Now",
                ["Time_MinutesAgo"] = "{0} min ago",
                ["Time_HoursAgo"] = "{0} h ago",
                ["Time_DaysAgo"] = "{0} d ago",
                ["Time_Yesterday"] = "Yesterday",
                ["Time_Today"] = "Today",

                // === FOOTER ===
                ["Footer_Terms"] = "Terms",
                ["Footer_Privacy"] = "Privacy",
                ["Footer_Help"] = "Help",
                ["Footer_About"] = "About",
                ["Footer_Contact"] = "Contact",
                ["Footer_Copyright"] = "All rights reserved",
            };

            // ========================================
            // PORTUGUÊS (pt)
            // ========================================
            _translations["pt"] = new Dictionary<string, string>
            {
                // === NAVEGAÇÃO ===
                ["Nav_Feed"] = "Feed",
                ["Nav_Create"] = "Criar",
                ["Nav_Search"] = "Buscar...",
                ["Nav_Notifications"] = "Notificacoes",
                ["Nav_Messages"] = "Mensagens",
                ["Nav_Profile"] = "Perfil",
                ["Nav_Settings"] = "Configuracoes",
                ["Nav_Dashboard"] = "Painel",
                ["Nav_Wallet"] = "Carteira",
                ["Nav_Logout"] = "Sair",
                ["Nav_Login"] = "Entrar",
                ["Nav_Register"] = "Cadastrar",
                ["Nav_Explore"] = "Explorar",
                ["Nav_Home"] = "Inicio",
                ["Nav_Admin"] = "Admin",
                ["Nav_Challenges"] = "Desafios",
                ["Nav_Favorites"] = "Favoritos",
                ["Nav_Subscriptions"] = "Inscricoes",
                ["Nav_Statistics"] = "Estatisticas",
                ["Nav_Activity"] = "Atividade",
                ["Nav_MyContent"] = "Meu Conteudo",
                ["Nav_MyAgency"] = "Minha Agencia",
                ["Nav_Help"] = "Central de Ajuda",

                // === AUTENTICAÇÃO ===
                ["Auth_Login"] = "Entrar",
                ["Auth_Register"] = "Cadastrar",
                ["Auth_Email"] = "E-mail",
                ["Auth_Password"] = "Senha",
                ["Auth_ConfirmPassword"] = "Confirmar senha",
                ["Auth_FullName"] = "Nome completo",
                ["Auth_Username"] = "Nome de usuario",
                ["Auth_RememberMe"] = "Lembrar de mim",
                ["Auth_ForgotPassword"] = "Esqueci minha senha",
                ["Auth_NoAccount"] = "Nao tem uma conta?",
                ["Auth_HaveAccount"] = "Ja tem uma conta?",
                ["Auth_LoginWithGoogle"] = "Continuar com Google",
                ["Auth_Or"] = "ou",
                ["Auth_Welcome"] = "Bem-vindo",
                ["Auth_WelcomeBack"] = "Bem-vindo de volta",
                ["Auth_CreateAccount"] = "Criar sua conta",
                ["Auth_LoginSubtitle"] = "Digite suas credenciais para continuar",
                ["Auth_RegisterSubtitle"] = "Preencha seus dados para comecar",
                ["Auth_PasswordRequirements"] = "Minimo 8 caracteres, incluir numeros",
                ["Auth_AcceptTerms"] = "Aceito os termos e condicoes",
                ["Auth_Over18"] = "Confirmo que tenho 18 anos ou mais",
                ["Auth_EmailOrUsername"] = "E-mail ou usuario",
                ["Auth_EmailPlaceholder"] = "voce@email.com",
                ["Auth_OrContinueWith"] = "ou continue com",

                // === LOGIN PAGE ===
                ["Login_VisualTitle"] = "Seu outro lado te espera",
                ["Login_VisualSubtitle"] = "O espaco onde voce nao precisa fingir. Compartilhe quem voce realmente e, sem filtros ou julgamentos.",
                ["Login_FeaturePrivate"] = "Privado",
                ["Login_FeatureNoCensorship"] = "Sem censura",
                ["Login_FeatureMonetize"] = "Monetize",
                ["Login_Beta"] = "Em Beta",

                // === REGISTER PAGE ===
                ["Register_DualIdentity"] = "Dupla identidade criativa",
                ["Register_DualIdentityDesc"] = "Publique conteudo gratuito com seu nome real e conteudo premium com seu pseudonimo.",
                ["Register_AccountDetails"] = "Dados da conta",
                ["Register_UsernamePlaceholder"] = "usuario123",
                ["Register_RealIdentity"] = "sua identidade real",
                ["Register_FullNamePlaceholder"] = "Joao Silva",
                ["Register_Pseudonym"] = "Pseudonimo",
                ["Register_ForPremiumContent"] = "para conteudo premium",
                ["Register_PseudonymPlaceholder"] = "OMisterioso",
                ["Register_PwdReq8Chars"] = "Minimo de 8 caracteres",
                ["Register_PwdReqNumber"] = "Deve conter pelo menos 1 numero ou simbolo",
                ["Register_PwdReqLetter"] = "Deve conter pelo menos 1 letra",
                ["Register_AcceptTermsText"] = "Ao criar uma conta, voce aceita a",
                ["Register_PrivacyPolicy"] = "Politica de Privacidade",
                ["Register_RegistrationAgreement"] = "Acordo de Registro",
                ["Register_CreateMyAccount"] = "Criar Minha Conta",

                // === FEED ===
                ["Feed_Title"] = "Feed",
                ["Feed_EmptyState"] = "Nenhuma publicacao para mostrar",
                ["Feed_EmptyStateSubtitle"] = "Siga criadores para ver o conteudo deles aqui",
                ["Feed_Like"] = "Curtir",
                ["Feed_Comment"] = "Comentar",
                ["Feed_Share"] = "Compartilhar",
                ["Feed_Save"] = "Salvar",
                ["Feed_Follow"] = "Seguir",
                ["Feed_Following"] = "Seguindo",
                ["Feed_Unfollow"] = "Deixar de seguir",
                ["Feed_Subscribe"] = "Inscrever-se",
                ["Feed_Subscribed"] = "Inscrito",
                ["Feed_ViewPost"] = "Ver publicacao",
                ["Feed_CopyLink"] = "Copiar link",
                ["Feed_Report"] = "Denunciar",
                ["Feed_Block"] = "Bloquear usuario",
                ["Feed_AddToFavorites"] = "Adicionar aos favoritos",
                ["Feed_Premium"] = "Conteudo Premium",
                ["Feed_Unlock"] = "Desbloquear",
                ["Feed_SubscribeToSee"] = "Inscreva-se para ver este conteudo",
                ["Feed_SuggestionsForYou"] = "Sugestoes para voce",
                ["Feed_PremiumCreators"] = "Criadores Premium",
                ["Feed_Trending"] = "Tendencias",
                ["Feed_WriteComment"] = "Escreva um comentario...",
                ["Feed_Send"] = "Enviar",
                ["Feed_ViewAllComments"] = "Ver todos os comentarios",
                ["Feed_Stories"] = "Stories",
                ["Feed_Collections"] = "Colecoes",
                ["Feed_LadoA"] = "Lado A",
                ["Feed_LadoB"] = "Lado B",

                // === CONTEÚDO ===
                ["Content_Create"] = "Criar conteudo",
                ["Content_Upload"] = "Enviar",
                ["Content_Description"] = "Descricao",
                ["Content_DescriptionPlaceholder"] = "Escreva algo sobre seu conteudo...",
                ["Content_SelectFile"] = "Selecionar arquivo",
                ["Content_DragDrop"] = "Arraste e solte seu arquivo aqui",
                ["Content_Photo"] = "Foto",
                ["Content_Video"] = "Video",
                ["Content_Audio"] = "Audio",
                ["Content_Text"] = "Texto",
                ["Content_Price"] = "Preco",
                ["Content_Free"] = "Gratis",
                ["Content_Premium"] = "Premium",
                ["Content_Public"] = "Publico",
                ["Content_Private"] = "Privado",
                ["Content_Publish"] = "Publicar",
                ["Content_SaveDraft"] = "Salvar rascunho",
                ["Content_Schedule"] = "Agendar",
                ["Content_Delete"] = "Excluir",
                ["Content_Edit"] = "Editar",
                ["Content_Preview"] = "Pre-visualizar",

                // === PERFIL ===
                ["Profile_Title"] = "Perfil",
                ["Profile_MyProfile"] = "Meu Perfil",
                ["Profile_EditProfile"] = "Editar perfil",
                ["Profile_Posts"] = "Publicacoes",
                ["Profile_Followers"] = "Seguidores",
                ["Profile_Following"] = "Seguindo",
                ["Profile_Bio"] = "Biografia",
                ["Profile_Verified"] = "Verificado",
                ["Profile_Creator"] = "Criador",
                ["Profile_Fan"] = "Fa",
                ["Profile_JoinedOn"] = "Entrou em",
                ["Profile_Location"] = "Localizacao",
                ["Profile_Website"] = "Site",
                ["Profile_SubscriptionPrice"] = "Preco da inscricao",
                ["Profile_PerMonth"] = "/mes",

                // === CONFIGURAÇÕES ===
                ["Settings_Title"] = "Configuracoes",
                ["Settings_Account"] = "Conta",
                ["Settings_Security"] = "Seguranca",
                ["Settings_Notifications"] = "Notificacoes",
                ["Settings_Privacy"] = "Privacidade",
                ["Settings_Language"] = "Idioma",
                ["Settings_Theme"] = "Tema",
                ["Settings_DarkMode"] = "Modo escuro",
                ["Settings_LightMode"] = "Modo claro",
                ["Settings_SystemTheme"] = "Tema do sistema",
                ["Settings_Save"] = "Salvar alteracoes",
                ["Settings_Cancel"] = "Cancelar",
                ["Settings_ChangePassword"] = "Alterar senha",
                ["Settings_CurrentPassword"] = "Senha atual",
                ["Settings_NewPassword"] = "Nova senha",
                ["Settings_ConfirmNewPassword"] = "Confirmar nova senha",
                ["Settings_DeleteAccount"] = "Excluir conta",
                ["Settings_DeleteAccountWarning"] = "Esta acao e irreversivel",
                ["Settings_BlockedUsers"] = "Usuarios bloqueados",
                ["Settings_BlockedUsersDesc"] = "Gerencie sua lista de usuarios bloqueados",
                ["Settings_ViewBlocked"] = "Ver bloqueados",
                ["Settings_SelectLanguage"] = "Selecione seu idioma preferido",
                ["Settings_LanguageSpanish"] = "Espanhol (Latino)",
                ["Settings_LanguageEnglish"] = "Ingles",
                ["Settings_LanguagePortuguese"] = "Portugues",

                // === MENSAGENS ===
                ["Messages_Title"] = "Mensagens",
                ["Messages_NewMessage"] = "Nova mensagem",
                ["Messages_Search"] = "Buscar conversa...",
                ["Messages_NoMessages"] = "Voce nao tem mensagens",
                ["Messages_TypeMessage"] = "Digite uma mensagem...",
                ["Messages_Send"] = "Enviar",
                ["Messages_Online"] = "Online",
                ["Messages_Offline"] = "Offline",
                ["Messages_LastSeen"] = "Visto por ultimo",
                ["Messages_Unread"] = "Nao lido",

                // === CARTEIRA ===
                ["Wallet_Title"] = "Carteira",
                ["Wallet_Balance"] = "Saldo disponivel",
                ["Wallet_Earnings"] = "Ganhos",
                ["Wallet_ThisMonth"] = "Este mes",
                ["Wallet_Withdraw"] = "Sacar",
                ["Wallet_AddFunds"] = "Adicionar fundos",
                ["Wallet_Transactions"] = "Transacoes",
                ["Wallet_NoTransactions"] = "Sem transacoes",
                ["Wallet_Pending"] = "Pendente",
                ["Wallet_Completed"] = "Concluido",
                ["Wallet_Failed"] = "Falhou",

                // === PAINEL ===
                ["Dashboard_Title"] = "Painel",
                ["Dashboard_Overview"] = "Resumo",
                ["Dashboard_Subscribers"] = "Inscritos",
                ["Dashboard_Revenue"] = "Receita",
                ["Dashboard_Views"] = "Visualizacoes",
                ["Dashboard_Engagement"] = "Engajamento",
                ["Dashboard_Growth"] = "Crescimento",
                ["Dashboard_Analytics"] = "Analiticas",

                // === INSCRIÇÕES ===
                ["Subscription_Subscribe"] = "Inscrever-se",
                ["Subscription_Unsubscribe"] = "Cancelar inscricao",
                ["Subscription_Renew"] = "Renovar",
                ["Subscription_Expires"] = "Expira em",
                ["Subscription_Active"] = "Ativa",
                ["Subscription_Expired"] = "Expirada",
                ["Subscription_Cancelled"] = "Cancelada",
                ["Subscription_Monthly"] = "Mensal",
                ["Subscription_Yearly"] = "Anual",
                ["Subscription_Benefits"] = "Beneficios",
                ["Subscription_IncludesLadoB"] = "Inclui conteudo Lado B",

                // === DESAFIOS ===
                ["Challenge_Title"] = "Desafios",
                ["Challenge_Create"] = "Criar desafio",
                ["Challenge_Budget"] = "Orcamento",
                ["Challenge_Deadline"] = "Prazo",
                ["Challenge_Participants"] = "Participantes",
                ["Challenge_Submit"] = "Enviar proposta",
                ["Challenge_Pending"] = "Pendente",
                ["Challenge_Accepted"] = "Aceito",
                ["Challenge_Completed"] = "Concluido",
                ["Challenge_Rejected"] = "Rejeitado",
                ["Challenge_PublicFeed"] = "Feed Publico",
                ["Challenge_MyChallenges"] = "Meus Desafios",
                ["Challenge_Received"] = "Desafios Recebidos",
                ["Challenge_MyProposals"] = "Minhas Propostas",

                // === AGÊNCIA ===
                ["Agency_MyAds"] = "Meus Anuncios",
                ["Agency_CreateAd"] = "Criar Anuncio",
                ["Agency_Recharge"] = "Recarregar Saldo",

                // === BOTÕES COMUNS ===
                ["Button_Save"] = "Salvar",
                ["Button_Cancel"] = "Cancelar",
                ["Button_Delete"] = "Excluir",
                ["Button_Edit"] = "Editar",
                ["Button_Submit"] = "Enviar",
                ["Button_Close"] = "Fechar",
                ["Button_Confirm"] = "Confirmar",
                ["Button_Back"] = "Voltar",
                ["Button_Next"] = "Proximo",
                ["Button_Previous"] = "Anterior",
                ["Button_See_More"] = "Ver mais",
                ["Button_See_Less"] = "Ver menos",
                ["Button_Loading"] = "Carregando...",
                ["Button_Retry"] = "Tentar novamente",
                ["Button_ViewAll"] = "Ver tudo",

                // === MENSAGENS DO SISTEMA ===
                ["Msg_Success"] = "Operacao bem-sucedida",
                ["Msg_Error"] = "Ocorreu um erro",
                ["Msg_Saved"] = "Salvo com sucesso",
                ["Msg_Deleted"] = "Excluido com sucesso",
                ["Msg_Updated"] = "Atualizado com sucesso",
                ["Msg_Copied"] = "Copiado para a area de transferencia",
                ["Msg_Loading"] = "Carregando...",
                ["Msg_NoResults"] = "Nenhum resultado encontrado",
                ["Msg_ConfirmDelete"] = "Tem certeza que deseja excluir?",
                ["Msg_ConfirmAction"] = "Tem certeza?",
                ["Msg_SessionExpired"] = "Sua sessao expirou",
                ["Msg_NetworkError"] = "Erro de conexao",
                ["Msg_InvalidCredentials"] = "Credenciais invalidas",
                ["Msg_RequiredField"] = "Este campo e obrigatorio",
                ["Msg_InvalidEmail"] = "E-mail invalido",
                ["Msg_PasswordMismatch"] = "As senhas nao coincidem",
                ["Msg_UserBlocked"] = "Voce bloqueou este usuario",
                ["Msg_CantSendMessage"] = "Voce nao pode enviar mensagens para este usuario",
                ["Msg_LanguageChanged"] = "Idioma alterado com sucesso",

                // === TEMPO ===
                ["Time_Now"] = "Agora",
                ["Time_MinutesAgo"] = "ha {0} min",
                ["Time_HoursAgo"] = "ha {0} h",
                ["Time_DaysAgo"] = "ha {0} d",
                ["Time_Yesterday"] = "Ontem",
                ["Time_Today"] = "Hoje",

                // === RODAPÉ ===
                ["Footer_Terms"] = "Termos",
                ["Footer_Privacy"] = "Privacidade",
                ["Footer_Help"] = "Ajuda",
                ["Footer_About"] = "Sobre",
                ["Footer_Contact"] = "Contato",
                ["Footer_Copyright"] = "Todos os direitos reservados",
            };
        }

        public string Get(string key)
        {
            var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return GetForCulture(key, culture);
        }

        public string Get(string key, params object[] args)
        {
            var template = Get(key);
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                return template;
            }
        }

        public string GetForCulture(string key, string culture)
        {
            // Intentar obtener del idioma solicitado
            if (_translations.TryGetValue(culture, out var translations) &&
                translations.TryGetValue(key, out var value))
            {
                return value;
            }

            // Fallback a español
            if (_translations.TryGetValue("es", out var esTranslations) &&
                esTranslations.TryGetValue(key, out var esValue))
            {
                return esValue;
            }

            // Si no existe, devolver la key
            return key;
        }

        public IReadOnlyDictionary<string, string> GetAllTranslations()
        {
            var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (_translations.TryGetValue(culture, out var translations))
            {
                return translations;
            }
            return _translations["es"];
        }
    }

    /// <summary>
    /// Proveedor de cultura basado en el idioma del usuario autenticado
    /// </summary>
    public class UserLanguageRequestCultureProvider : RequestCultureProvider
    {
        public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
        {
            // Verificar si el usuario está autenticado
            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                var userManager = httpContext.RequestServices
                    .GetService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();

                if (userManager != null)
                {
                    var user = await userManager.GetUserAsync(httpContext.User);
                    if (user != null && !string.IsNullOrEmpty(user.Idioma))
                    {
                        return new ProviderCultureResult(user.Idioma);
                    }
                }
            }

            // Verificar cookie de idioma para usuarios no autenticados
            if (httpContext.Request.Cookies.TryGetValue("Lado.Language", out var langCookie) &&
                !string.IsNullOrEmpty(langCookie))
            {
                return new ProviderCultureResult(langCookie);
            }

            // Intentar obtener del header Accept-Language
            var acceptLanguage = httpContext.Request.Headers["Accept-Language"].ToString();
            if (!string.IsNullOrEmpty(acceptLanguage))
            {
                var preferredLang = acceptLanguage.Split(',').FirstOrDefault()?.Split('-').FirstOrDefault();
                if (!string.IsNullOrEmpty(preferredLang))
                {
                    // Solo aceptar idiomas soportados
                    if (preferredLang == "es" || preferredLang == "en" || preferredLang == "pt")
                    {
                        return new ProviderCultureResult(preferredLang);
                    }
                }
            }

            // Default: español
            return new ProviderCultureResult("es");
        }
    }
}
