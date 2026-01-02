using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Configuración administrativa del sistema de LadoCoins.
    /// Permite ajustar montos de bonos sin necesidad de recompilar.
    /// </summary>
    public class ConfiguracionLadoCoin
    {
        public int Id { get; set; }

        /// <summary>
        /// Clave única de la configuración (usar constantes definidas abajo)
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Clave { get; set; } = string.Empty;

        /// <summary>
        /// Valor numérico de la configuración
        /// </summary>
        [Display(Name = "Valor")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Valor { get; set; }

        /// <summary>
        /// Descripción legible de la configuración
        /// </summary>
        [Display(Name = "Descripción")]
        [StringLength(200)]
        public string? Descripcion { get; set; }

        /// <summary>
        /// Si la configuración está activa
        /// </summary>
        [Display(Name = "Activo")]
        public bool Activo { get; set; } = true;

        /// <summary>
        /// Categoría para agrupar en la UI de admin
        /// </summary>
        [Display(Name = "Categoría")]
        [StringLength(50)]
        public string? Categoria { get; set; }

        [Display(Name = "Fecha Modificación")]
        public DateTime FechaModificacion { get; set; } = DateTime.Now;

        [Display(Name = "Modificado Por")]
        [StringLength(100)]
        public string? ModificadoPor { get; set; }

        // ========================================
        // CONSTANTES DE CLAVES
        // ========================================

        // Bonos de registro y perfil
        public const string BONO_BIENVENIDA = "BonoBienvenida";                    // $20
        public const string BONO_PRIMER_CONTENIDO = "BonoPrimerContenido";         // $5
        public const string BONO_VERIFICAR_EMAIL = "BonoVerificarEmail";           // $2
        public const string BONO_COMPLETAR_PERFIL = "BonoCompletarPerfil";         // $3

        // Bonos diarios
        public const string BONO_LOGIN_DIARIO = "BonoLoginDiario";                 // $0.50
        public const string BONO_CONTENIDO_DIARIO = "BonoContenidoDiario";         // $1
        public const string BONO_5_LIKES = "Bono5Likes";                           // $0.25
        public const string BONO_3_COMENTARIOS = "Bono3Comentarios";               // $0.50
        public const string BONO_RACHA_7_DIAS = "BonoRacha7Dias";                  // $5

        // Bonos de referidos
        public const string BONO_REFERIDOR = "BonoReferidor";                      // $10
        public const string BONO_REFERIDO = "BonoReferido";                        // $15
        public const string BONO_REFERIDO_CREADOR = "BonoReferidoCreador";         // $50
        public const string COMISION_REFERIDO_PORCENTAJE = "ComisionReferidoPorcentaje"; // 10%
        public const string COMISION_REFERIDO_MESES = "ComisionReferidoMeses";     // 3 meses

        // Sistema de monedas
        public const string PORCENTAJE_QUEMA = "PorcentajeQuema";                  // 5%
        public const string DIAS_VENCIMIENTO = "DiasVencimiento";                  // 30 días
        public const string MAX_PORCENTAJE_SUSCRIPCION = "MaxPorcentajeSuscripcion"; // 30%
        public const string MAX_PORCENTAJE_PROPINA = "MaxPorcentajePropina";       // 100%

        // Multiplicadores de canje
        public const string MULTIPLICADOR_PUBLICIDAD = "MultiplicadorPublicidad"; // 1.5x
        public const string MULTIPLICADOR_BOOST = "MultiplicadorBoost";           // 2x

        // Límites de seguridad
        public const string MAX_PREMIO_DIARIO = "MaxPremioDiario";                 // Límite de LadoCoins por día
        public const string MAX_PREMIO_MENSUAL = "MaxPremioMensual";               // Límite de LadoCoins por mes

        // ========================================
        // VALORES DEFAULT (para seed)
        // ========================================

        public static List<ConfiguracionLadoCoin> ObtenerValoresDefault()
        {
            return new List<ConfiguracionLadoCoin>
            {
                // Bonos de registro
                new() { Clave = BONO_BIENVENIDA, Valor = 20, Descripcion = "Bono de bienvenida al registrarse", Categoria = "Registro" },
                new() { Clave = BONO_PRIMER_CONTENIDO, Valor = 5, Descripcion = "Bono por primera publicación", Categoria = "Registro" },
                new() { Clave = BONO_VERIFICAR_EMAIL, Valor = 2, Descripcion = "Bono por verificar email", Categoria = "Registro" },
                new() { Clave = BONO_COMPLETAR_PERFIL, Valor = 3, Descripcion = "Bono por completar perfil", Categoria = "Registro" },

                // Bonos diarios
                new() { Clave = BONO_LOGIN_DIARIO, Valor = 0.50m, Descripcion = "Bono por login diario", Categoria = "Diario" },
                new() { Clave = BONO_CONTENIDO_DIARIO, Valor = 1, Descripcion = "Bono por subir contenido (1/día)", Categoria = "Diario" },
                new() { Clave = BONO_5_LIKES, Valor = 0.25m, Descripcion = "Bono por dar 5 likes", Categoria = "Diario" },
                new() { Clave = BONO_3_COMENTARIOS, Valor = 0.50m, Descripcion = "Bono por 3 comentarios", Categoria = "Diario" },
                new() { Clave = BONO_RACHA_7_DIAS, Valor = 5, Descripcion = "Bono por racha de 7 días", Categoria = "Diario" },

                // Referidos
                new() { Clave = BONO_REFERIDOR, Valor = 10, Descripcion = "Bono para quien invita", Categoria = "Referidos" },
                new() { Clave = BONO_REFERIDO, Valor = 15, Descripcion = "Bono para quien es invitado", Categoria = "Referidos" },
                new() { Clave = BONO_REFERIDO_CREADOR, Valor = 50, Descripcion = "Bono cuando referido crea en LadoB", Categoria = "Referidos" },
                new() { Clave = COMISION_REFERIDO_PORCENTAJE, Valor = 10, Descripcion = "% de comisión de premios del referido", Categoria = "Referidos" },
                new() { Clave = COMISION_REFERIDO_MESES, Valor = 3, Descripcion = "Meses de duración de comisión", Categoria = "Referidos" },

                // Sistema
                new() { Clave = PORCENTAJE_QUEMA, Valor = 5, Descripcion = "% de quema por transacción", Categoria = "Sistema" },
                new() { Clave = DIAS_VENCIMIENTO, Valor = 30, Descripcion = "Días hasta vencimiento", Categoria = "Sistema" },
                new() { Clave = MAX_PORCENTAJE_SUSCRIPCION, Valor = 30, Descripcion = "% máximo de LC en suscripciones", Categoria = "Sistema" },
                new() { Clave = MAX_PORCENTAJE_PROPINA, Valor = 100, Descripcion = "% máximo de LC en propinas", Categoria = "Sistema" },

                // Multiplicadores
                new() { Clave = MULTIPLICADOR_PUBLICIDAD, Valor = 1.5m, Descripcion = "$1 LC = $1.50 en ads", Categoria = "Canje" },
                new() { Clave = MULTIPLICADOR_BOOST, Valor = 2, Descripcion = "$1 LC = $2 en boost", Categoria = "Canje" },

                // Límites
                new() { Clave = MAX_PREMIO_DIARIO, Valor = 50, Descripcion = "Máximo LC ganables por día", Categoria = "Limites" },
                new() { Clave = MAX_PREMIO_MENSUAL, Valor = 500, Descripcion = "Máximo LC ganables por mes", Categoria = "Limites" }
            };
        }
    }
}
