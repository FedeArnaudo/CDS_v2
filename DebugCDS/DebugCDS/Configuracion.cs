using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DebugCDS
{
    class Configuracion
    {
        public enum TipoControlador
        {
            CEM = 0,
            WOLF,
            VOX,
            MIDEX,
            PAM
        }

        public class InfoConfig
        {
            public InfoConfig()
            {
                tipo = TipoControlador.CEM;
                ip = "";
                protocoloSurtidores = 16;
                rutaProyNuevo = "";
                segundosFacturacion = 0;
                nivelLog = Log.LogType.t_debug;
            }
            public TipoControlador tipo { get; set; }
            public string ip { get; set; }
            public int protocoloSurtidores { get; set; }
            public string rutaProyNuevo { get; set; }
            public int segundosFacturacion { get; set; }
            public Log.LogType nivelLog { get; set; }
            public string bd { get; set; }
        }

        //private static readonly string configFile = Environment.CurrentDirectory + "/config.ini";
        private static readonly string configFile = AppDomain.CurrentDomain.BaseDirectory + "/config.ini";

        static public InfoConfig leerConfiguracion()
        {
            InfoConfig internalConfig;
            try
            {
                StreamReader reader;
                try
                {
                    reader = new StreamReader(configFile);
                }
                catch (Exception e)
                {
                    Log.Instance.writeLog("Error al leer archivo de configuración. Excepción: " + e.Message, Log.LogType.t_error);
                    return null;
                }

                internalConfig = new InfoConfig();

                internalConfig.tipo = (TipoControlador)Convert.ToInt32(reader.ReadLine());
                internalConfig.ip = reader.ReadLine().Trim();
                internalConfig.protocoloSurtidores = Convert.ToInt32(reader.ReadLine());
                internalConfig.rutaProyNuevo = reader.ReadLine().Trim();
                internalConfig.segundosFacturacion = Convert.ToInt32(reader.ReadLine());
                internalConfig.nivelLog = (Log.LogType)Convert.ToInt32(reader.ReadLine());
                internalConfig.bd = reader.ReadLine();

                Log.Instance.setLogLevel(internalConfig.nivelLog);

                reader.Close();
            }
            catch (Exception e)
            {
                Log.Instance.writeLog("Error al leer archivo de configuración. Formato incorrecto. Excepción: " + e.Message, Log.LogType.t_error);
                return null;
            }
            return internalConfig;
        }

        static public bool guardarConfiguracion(InfoConfig info)
        {
            try
            {
                Log.Instance.setLogLevel(info.nivelLog);
                using (StreamWriter outputFile = new StreamWriter(configFile, false))
                {
                    outputFile.WriteLine(((int)info.tipo).ToString());
                    outputFile.WriteLine(info.ip.Trim());
                    outputFile.WriteLine(info.protocoloSurtidores.ToString());
                    outputFile.WriteLine(info.rutaProyNuevo.Trim());
                    outputFile.WriteLine(info.segundosFacturacion.ToString());
                    outputFile.WriteLine(((int)info.nivelLog).ToString());
                    outputFile.WriteLine(info.bd);
                }
            }
            catch (Exception e)
            {
                Log.Instance.writeLog("Error al guardar la configuración. Excepción: " + e.Message, Log.LogType.t_error);
                return false;
            }
            return true;
        }

        static public bool existeConfiguracion()
        {
            if (File.Exists(configFile))
            {
                return true;
            }
            return false;
        }
    }
}
