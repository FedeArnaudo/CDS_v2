using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DebugCDS
{
    abstract class Conector
    {
        #region Estructuras
        public class Despacho
        {
            public enum EstadoDespacho
            {
                DISPONIBLE,
                EN_SOLICITUD,
                DESPACHANDO,
                AUTORIZADO,
                VENTA_FINALIZADA_IMPAGA,
                DEFECTUOSO,
                ANULADO,
                DETENIDO
            }
            public EstadoDespacho status { get; set; }
            public int nroVenta { get; set; }
            public int producto { get; set; }
            public string monto { get; set; }
            public string volumen { get; set; }
            public string PPU { get; set; }
            public bool facturada { get; set; }
            public string id { get; set; }

            public EstadoDespacho status_old { get; set; }
            public int nroVenta_old { get; set; }
            public int producto_old { get; set; }
            public string monto_old { get; set; }
            public string volumen_old { get; set; }
            public string PPU_old { get; set; }
            public bool facturada_old { get; set; }
            public string id_old { get; set; }
        }

        public class Cierre
        {
            public class Total
            {
                public string monto { get; set; }
                public string volumen { get; set; }
            }
            public class TotalMangueraSurtidor
            {
                public string idSurtidor { get; set; }
                public List<Total> total { get; set; }
            }
            public class TotalPorMangueraSinControl
            {
                public string idSurtidor { get; set; }
                public List<Total> total { get; set; }
            }
            public class TotalPorMangueraPruebas
            {
                public string idSurtidor { get; set; }
                public List<Total> total { get; set; }
            }
            public class TotalPorProducto
            {
                public string monto { get; set; }
                public string volumen { get; set; }
                public string precio { get; set; }
            }
            public Cierre()
            {
                totalesMediosDePago = new List<Total>();
                totalesPorProducto = new List<TotalPorProducto>();
                totalesPorMangueraPorSurtidor = new List<TotalMangueraSurtidor>();
                totalesPorMangueraSinControl = new List<TotalPorMangueraSinControl>();
                totalesPorMangueraPruebas = new List<TotalPorMangueraPruebas>();
                tanques = new List<Tanque>();
                productos = new List<Producto>();
                totalesPorPeriodoPorNivelPorProducto = new List<List<List<Total>>>();
            }

            public List<Total> totalesMediosDePago;
            public string impuesto1;
            public string impuesto2;
            public int nivelesDePrecios;
            public int periodosDePrecios;
            public List<List<List<Total>>> totalesPorPeriodoPorNivelPorProducto;
            public List<TotalPorProducto> totalesPorProducto;
            public List<TotalMangueraSurtidor> totalesPorMangueraPorSurtidor;
            public List<TotalPorMangueraSinControl> totalesPorMangueraSinControl;
            public List<TotalPorMangueraPruebas> totalesPorMangueraPruebas;
            public List<Tanque> tanques;
            public List<Producto> productos;
        }



        public class Tanque
        {
            public string producto { get; set; }
            public string agua { get; set; }
            public string vacio { get; set; }
            public string capacidad { get; set; }
        }

        public class Producto
        {
            public string id { get; set; }
            public string precio { get; set; }
            public string volumen { get; set; }
            public string agua { get; set; }
            public string vacio { get; set; }
            public string capacidad { get; set; }
        }

        public class Surtidor
        {
            public Surtidor()
            {
                productoPorManguera = new List<int>();
            }
            public int nivelDePrecios { get; set; }
            public List<int> productoPorManguera { get; set; }
        }

        public class ConfiguracionEstacion
        {
            public ConfiguracionEstacion()
            {
                productos = new List<Producto>();
                surtidores = new List<Surtidor>();
                productoPorTanque = new List<int>();
            }
            public int protocolo { get; set; }
            public List<Surtidor> surtidores { get; set; }
            public List<int> productoPorTanque { get; set; }
            public List<Producto> productos { get; set; }
        }
        #endregion

        #region Métodos
        /// <summary>
        /// Este metodo no se usa en el programa, pero esta disponible por
        /// si es necesario para debug del controlador.
        /// </summary>
        /// <returns> true si se pudo conectar correctamente </returns>
        public abstract bool checkConexion();

        /// <summary>
        /// Trae el despacho del surtidor solicitado y crea una estructura
        /// para almacenar los datos de ese despacho
        /// </summary>
        /// <param name="surtidor"> Número de surtidor </param>
        /// <returns> Estructura que contiene los datos de ese despacho </returns>
        public abstract Despacho traerDespacho(int surtidor);

        /// <summary>
        /// Trae los datos del turno actual, no se utiliza en producción,
        /// pero sirve para probar la estructura de datos del cierre de turno.
        /// </summary>
        /// <returns> Una estructura que contiene los datos del turno actual </returns>
        public abstract Cierre traerTurnoEnCurso();

        /// <summary>
        /// Hace un cierre de turno y devuelve los datos del mismo.
        /// </summary>
        /// <returns> Una estructura que contiene los datos del cierre de turno </returns>
        public abstract Cierre cierreDeTurno();

        /// <summary>
        /// Solicita la información de la configuracion de la estacion (cantidad de surtidores,
        /// productos, tanques, mangueras, etc.) y carga los mismos ne una estructura de datos.
        /// </summary>
        /// <returns> Una estructura con la configuración de la estación </returns>
        public abstract ConfiguracionEstacion configuracionDeEstacion();

        /// <summary>
        /// Consulta el volumen de los tanques y crea una lista con la información obtenida.
        /// </summary>
        /// <param name="cantidadTanques"> Cantidad de tanques configurados </param>
        /// <returns> Una lista de tanques con el volumen actual y total de cada uno </returns>
        public abstract List<Tanque> traerTanques(int cantidadTanques);

        /// <summary>
        /// Método de uso interno para ejecutar comandos del tipo byte
        /// </summary>
        /// <param name="comando"> Comando a ejecutar </param>
        /// <returns> Respuesta al comando </returns>
        protected abstract byte[] enviarComando(byte[] comando);
        #endregion
    }

    class ConectorCEM : Conector
    {
        #region Comandos
        struct ComandoCHECK
        {
            public static byte[] mensaje = new byte[] { 0x0 };
            public static byte[] respuesta = new byte[] { 0x0 };
        }

        struct ComandoConfigEstacion
        {
            public static byte[] mensaje = new byte[] { 0x65 };
            public static int confirmacion = 0;
            public static int surtidores = 1;
            public static int tanques = 3;
            public static int productos = 4;
        }

        struct ComandoTraerDespacho
        {
            public static byte[] mensaje = new byte[] { 0x70 };
            public static int confirmacion = 0;
            public static int status = 1;
            public static int nro_venta = 2;
            public static int codigo_producto = 3;
        }

        struct ComandoTraerTanques
        {
            public static byte[] mensaje = new byte[] { 0x68 };
            public static int confirmacion = 0;
        }

        struct ComandoTurnoEnCurso
        {
            public static byte[] mensaje = new byte[] { 0x08 };
            public static int estadoTurno = 0;
        }

        struct ComandoCierreDeTurno
        {
            public static byte[] mensaje = new byte[] { 0x01 };
            public static int estadoTurno = 0;
        }

        #endregion

        public ConectorCEM()
        {
            var config = Configuracion.leerConfiguracion();
            if (config.protocoloSurtidores == 16)
            {
                ComandoConfigEstacion.mensaje = new byte[] { 0x65 };
                ComandoTraerDespacho.mensaje = new byte[] { 0x70 };
                ComandoTraerTanques.mensaje = new byte[] { 0x68 };
            }
            else
            {
                ComandoConfigEstacion.mensaje = new byte[] { 0xB5 };
                ComandoTraerDespacho.mensaje = new byte[] { 0xC0 };
                ComandoTraerTanques.mensaje = new byte[] { 0xB8 };
            }
        }

        private readonly byte separador = 0x7E;

        private readonly string nombreDelPipe = "CEM44POSPIPE";

        private void descartarCampoVariable(byte[] data, ref int pos)
        {
            while (data[pos] != separador)
                pos++;
            pos++;
        }

        private string leerCampoVariable(byte[] data, ref int pos)
        {
            string ret = "";
            ret += Encoding.ASCII.GetString(new byte[] { data[pos] });
            int i = pos + 1;
            while (data[i] != separador)
            {
                ret += Encoding.ASCII.GetString(new byte[] { data[i] });
                i++;
            }
            i++;
            pos = i;
            return ret;
        }

        protected override byte[] enviarComando(byte[] comando)
        {
            byte[] buffer;
            string ip = "ip";
                //Configuracion.leerConfiguracion().ip;

            try
            {
                using (var pipeClient = new NamedPipeClientStream(ip, nombreDelPipe))
                {
                    pipeClient.Connect();

                    pipeClient.Write(comando, 0, comando.Length);

                    buffer = new byte[pipeClient.OutBufferSize];

                    pipeClient.Read(buffer, 0, buffer.Length);
                }
            }
            catch (Exception e)
            {
                throw new Exception("Error al enviar el comando. Excepción: " + e.Message);
            }
            return buffer;
        }

        public override bool checkConexion()
        {
            byte[] res = enviarComando(ComandoCHECK.mensaje);

            if (res[0] == ComandoCHECK.respuesta[0])
                return true;

            return false;
        }

        public override Despacho traerDespacho(int surtidor)
        {
            Despacho ret = new Despacho();
            return ret;
        }

        public override Cierre traerTurnoEnCurso()
        {
            Cierre ret = new Cierre();
            return ret;
        }

        public override Cierre cierreDeTurno()
        {
            Cierre ret = new Cierre();
            return ret;
        }

        public override ConfiguracionEstacion configuracionDeEstacion()
        {
            ConfiguracionEstacion ret = new ConfiguracionEstacion();

            return ret;
        }

        public override List<Tanque> traerTanques(int cantidadTanques)
        {
            List<Tanque> ret = new List<Tanque>();
            return ret;
        }
    }
}
