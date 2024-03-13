using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DebugCDS
{
    abstract class Controlador
    {
        #region Estructuras
        public class Tanque
        {
            public int producto { get; set; }
        }

        public class Producto
        {
            public int id { get; set; }
            public double precio { get; set; }
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

        public class ConfigEstacion
        {
            public ConfigEstacion(Conector.ConfiguracionEstacion config)
            {
                protocolo = config.protocolo;
                surtidores = new List<Surtidor>();
                tanques = new List<Tanque>();
                productos = new List<Producto>();

                foreach (var surtidor in config.surtidores)
                {
                    Surtidor tmp = new Surtidor();
                    tmp.nivelDePrecios = surtidor.nivelDePrecios;
                    tmp.productoPorManguera = new List<int>();
                    tmp.productoPorManguera = surtidor.productoPorManguera;
                    surtidores.Add(tmp);
                }

                foreach (var producto in config.productoPorTanque)
                {
                    Tanque tmp = new Tanque();
                    tmp.producto = producto;
                    tanques.Add(tmp);
                }

                foreach (var producto in config.productos)
                {
                    Producto tmp = new Producto();
                    tmp.id = Convert.ToInt32(producto.id);
                    tmp.precio = Convert.ToDouble(producto.id);
                    productos.Add(tmp);
                }

            }

            public int protocolo { get; set; }
            public List<Surtidor> surtidores { get; set; }
            public List<Tanque> tanques { get; set; }
            public List<Producto> productos { get; set; }
        }
        #endregion

        #region Atributos
        // Instancia de Singleton
        static public Controlador instancia = null;
        // Instancia del conector que va a utilizar
        protected Conector conector;
        // Instancia de la configuración de la estación cargada en ejecución
        protected ConfigEstacion configEstacion;
        // Hilo para manejar el proceso principal de consulta al controlador en paralelo
        // al resto de la ejecución
        static private Thread procesoPrincipal = null;

        // Mutex para control del hilo del proceso principal
        static public Mutex working = new Mutex();
        // Tiempo de espera entre cada procesamiento en segundos.
        static private int loopDelaySeconds = 3;
        static public string pathArchivo = "";
        #endregion

        /// <summary>
        /// Metodo para obtener la instancia al conector
        /// </summary>
        /// <returns>Instancia de Conector</returns>
        static public Conector getConector()
        {
            return instancia.conector;
        }

        /// <summary>
        /// Este método consulta el último despacho al controlador y 
        /// lo graba en la base de datos SQLite
        /// </summary>
        public abstract void ultimoDespacho(Int32 idDespacho);
        /// <summary>
        /// Este método debe consultar los despachos al controlador y 
        /// grabarlos en la base de datos SQLite
        /// </summary>
        public abstract void grabarDespachos();

        /// <summary>
        /// Este método debe consultar el estado de los tanques y
        /// actualizar los valores en la base de datos (tabla tanques)
        /// </summary>
        public abstract void actualizarTanques();

        /// <summary>
        /// Verifica para cada despacho en la tabla despachos que tenga una fecha
        /// mas antigua que el valor cargado en la configuración de facturación automática y
        /// lo marca como facturado. En caso de que no esté habilitada la facturación
        /// automática no debe hacer nada este método.
        /// </summary>
        public abstract void facturaDespachos();

        /// <summary>
        /// Verifica la tabla cierreBandera y en caso de tener que hacer un cierre
        /// envía el comando correspondiente y graba la tabla cierres con los totales
        /// </summary>
        public abstract void compruebaCierre();

        /// <summary>
        /// Este método estático es el encargado de crear la instancia del controlador
        /// correspondiente y ejecutar el hilo del proceso automático
        /// </summary>
        /// <param name="config"> La configuración extraída del archivo de configuración </param>
        /// <returns> true si se pudo inicializar correctamente </returns>
        static public bool init(Configuracion.InfoConfig config)
        {
            if (instancia == null)
            {

                switch (config.tipo)
                {
                    case Configuracion.TipoControlador.CEM:
                        instancia = new ControladorCEM();
                        break;
                    default:
                        return false;
                }
            }

            if (pathArchivo == "" && procesoPrincipal == null)
            {
                procesoPrincipal = new Thread(new ThreadStart(loop));

                if (!procesoPrincipal.IsAlive)
                {
                    procesoPrincipal.Start();
                    Log.Instance.writeLog("Proceso de carga de despachos iniciado", Log.LogType.t_info);
                }
            }

            return true;
        }

        /// <summary>
        /// Método estático ejecutado en un hilo paralelo encargado del buble principal.
        /// </summary>
        static private void loop()
        {
            while (working.WaitOne(1))
            {
                try
                {
                    instancia.grabarDespachos();

                    instancia.actualizarTanques();

                    instancia.facturaDespachos();

                    instancia.compruebaCierre();

                    /// Devuelvo el mutex para escuchar eventos
                    working.ReleaseMutex();

                    /// Espera para procesar nuevamente
                    Thread.Sleep(loopDelaySeconds * 1000);
                }
                catch (Exception e)
                {
                    working.ReleaseMutex();
                    Log.Instance.writeLog("Error en el loop del controlador. Excepcion: " + e.Message, Log.LogType.t_error);
                }
            }
        }

        /// <summary>
        /// Este método estático crea una instancia del controlador correspondiente, 
        /// y lo deja en modo manual (a diferencia de la llamada al método "init")
        /// </summary>
        /// <param name="config"> La configuración extraída del archivo de configuración </param>
        /// <returns> true si se pudo inicializar correctamente </returns>
        static public bool configInit(Configuracion.InfoConfig config)
        {
            if (instancia != null)
                return true;

            switch (config.tipo)
            {
                case Configuracion.TipoControlador.CEM:
                    instancia = new ControladorCEM();
                    break;
                default:
                    return false;
            }

            return true;
        }
        /// <summary>
        /// Este método debe consultar el turno actual
        /// </summary>
        public abstract void ConsultarTurnoActual();
    }
    class ControladorCEM : Controlador
    {
        public ControladorCEM()
        {
            conector = new ConectorCEM();

            configEstacion = new ConfigEstacion(conector.configuracionDeEstacion());
        }

        public override void ultimoDespacho(Int32 idSurtidor)
        {
            // Traigo del CEM el despacho
            Conector.Despacho despacho = conector.traerDespacho(idSurtidor);

            if (despacho.status == Conector.Despacho.EstadoDespacho.DESPACHANDO || despacho.nroVenta == 0 || Convert.ToInt32(despacho.id.Trim().Substring(1)) == 0)
            {
                if (pathArchivo != "")
                {
                    string linea = "Off";
                    StreamWriter sw = new StreamWriter(pathArchivo);
                    sw.WriteLine(linea);
                    sw.Close();
                }
                return;
            }

            Log.Instance.writeLog("SELECT * FROM despachos WHERE surtidor = " + idSurtidor + " AND id = '" + despacho.id.Trim().Substring(1) + "'", Log.LogType.t_debug);
            DataTable tabla = ConectorSQLite.dt_query("SELECT * FROM despachos WHERE surtidor = " + idSurtidor + " AND id = '" + despacho.id.Trim().Substring(1) + "'");

            if (tabla.Rows.Count == 0)
            {
                string campos = "id,surtidor,producto,monto,volumen,ppu,YPFruta";

                DataTable producto = ConectorSQLite.dt_query(String.Format("SELECT id,precio FROM productos WHERE idSecundario = {0}", despacho.producto));

                int idProducto = 0;
                int YPFruta = 0;
                if (producto.Rows.Count != 0)
                {
                    idProducto = Convert.ToInt32(producto.Rows[0]["id"]);
                    if (Convert.ToDouble(producto.Rows[0]["precio"]) != Convert.ToDouble(despacho.PPU))
                        YPFruta = 1;
                }
                else
                    idProducto = despacho.producto;

                string valores = String.Format(
                    "'{0}',{1},{2},'{3}','{4}','{5}',{6}",
                    despacho.id.Trim().Substring(1),
                    idSurtidor,
                    idProducto,
                    despacho.monto.Trim(),
                    despacho.volumen.Trim(),
                    despacho.PPU.Trim(),
                    YPFruta);
                Log.Instance.writeLog("Insertando despacho " + despacho.id, Log.LogType.t_debug);
                ConectorSQLite.query(String.Format("INSERT INTO despachos ({0}) VALUES ({1})", campos, valores));
                if (pathArchivo != "")
                {
                    string linea = "";
                    StreamWriter sw = new StreamWriter(pathArchivo);
                    //linea = despacho.id.Trim() + "," +despacho.nroVenta.ToString()+","+ idSurtidor.ToString() + "," + idProducto.ToString() + "," + despacho.monto.Trim() + "," + despacho.volumen.Trim() + "," + despacho.PPU.Trim() + "," + YPFruta.ToString();
                    linea = despacho.id.Trim();
                    sw.WriteLine(linea);
                    linea = despacho.nroVenta.ToString();
                    sw.WriteLine(linea);
                    linea = idSurtidor.ToString();
                    sw.WriteLine(linea);
                    linea = idProducto.ToString();
                    sw.WriteLine(linea);
                    linea = despacho.monto.Trim();
                    sw.WriteLine(linea);
                    linea = despacho.volumen.Trim();
                    sw.WriteLine(linea);
                    linea = despacho.PPU.Trim();
                    sw.WriteLine(linea);
                    linea = YPFruta.ToString();
                    sw.WriteLine(linea);
                    sw.Close();
                }
            }
        }

        public override void grabarDespachos()
        {
            try
            {
                // Por cada surtidor grabamos el ultimo despacho que no esté grabado
                for (int surtidor = 1; surtidor < configEstacion.surtidores.Count + 1; surtidor++)
                {
                    // Traigo del CEM el despacho
                    Conector.Despacho despacho = conector.traerDespacho(surtidor);

                    if (despacho.status == Conector.Despacho.EstadoDespacho.DESPACHANDO || despacho.nroVenta == 0 || Convert.ToInt32(despacho.id.Trim().Substring(1)) == 0)
                        continue;

                    Log.Instance.writeLog("SELECT * FROM despachos WHERE surtidor = " + surtidor + " AND id = '" + despacho.id.Trim().Substring(1) + "'", Log.LogType.t_debug);
                    DataTable tabla = ConectorSQLite.dt_query("SELECT * FROM despachos WHERE surtidor = " + surtidor + " AND id = '" + despacho.id.Trim().Substring(1) + "'");

                    if (tabla.Rows.Count == 0)
                    {
                        string campos = "id,surtidor,producto,monto,volumen,ppu,YPFruta";

                        DataTable producto = ConectorSQLite.dt_query(String.Format("SELECT id,precio FROM productos WHERE idSecundario = {0}", despacho.producto));

                        int idProducto = 0;
                        int YPFruta = 0;
                        if (producto.Rows.Count != 0)
                        {
                            idProducto = Convert.ToInt32(producto.Rows[0]["id"]);
                            if (Convert.ToDouble(producto.Rows[0]["precio"]) != Convert.ToDouble(despacho.PPU))
                                YPFruta = 1;
                        }
                        else
                            idProducto = despacho.producto;

                        string valores = String.Format(
                            "'{0}',{1},{2},'{3}','{4}','{5}',{6}",
                            despacho.id.Trim().Substring(1),
                            surtidor,
                            idProducto,
                            despacho.monto.Trim(),
                            despacho.volumen.Trim(),
                            despacho.PPU.Trim(),
                            YPFruta);
                        Log.Instance.writeLog("Insertando despacho " + despacho.id, Log.LogType.t_debug);
                        ConectorSQLite.query(String.Format("INSERT INTO despachos ({0}) VALUES ({1})", campos, valores));
                        if (pathArchivo != "")
                        {
                            string linea = "";
                            StreamWriter sw = new StreamWriter(pathArchivo);
                            linea = despacho.id.Trim() + "," + surtidor.ToString() + "," + idProducto.ToString() + "," + despacho.monto.Trim() + "," + despacho.volumen.Trim() + "," + despacho.PPU.Trim() + "," + YPFruta.ToString();
                            sw.Close();
                        }
                    }

                    if (despacho.id_old == null || despacho.id_old == "")
                        return;

                    if (Convert.ToInt32(despacho.id_old.Trim().Substring(1)) == 0)
                        continue;

                    Log.Instance.writeLog("SELECT * FROM despachos WHERE surtidor = " + surtidor + " AND id = '" + despacho.id_old.Trim().Substring(1) + "'", Log.LogType.t_debug);
                    tabla = ConectorSQLite.dt_query("SELECT * FROM despachos WHERE surtidor = " + surtidor + " AND id = '" + despacho.id_old.Trim().Substring(1) + "'");

                    if (tabla.Rows.Count == 0)
                    {
                        string campos = "id,surtidor,producto,monto,volumen,ppu,YPFruta";

                        DataTable producto = ConectorSQLite.dt_query(String.Format("SELECT id,precio FROM productos WHERE idSecundario = {0}", despacho.producto_old));

                        int idProducto = 0;
                        int YPFruta = 0;
                        if (producto.Rows.Count != 0)
                        {
                            idProducto = Convert.ToInt32(producto.Rows[0]["id"]);
                            if (Convert.ToDouble(producto.Rows[0]["precio"]) != Convert.ToDouble(despacho.PPU_old))
                                YPFruta = 1;
                        }
                        else
                            idProducto = despacho.producto_old;

                        string valores = String.Format(
                            "'{0}',{1},{2},'{3}','{4}','{5}',{6}",
                            despacho.id_old.Trim().Substring(1),
                            surtidor,
                            idProducto,
                            despacho.monto_old.Trim(),
                            despacho.volumen_old.Trim(),
                            despacho.PPU_old.Trim(),
                            YPFruta);
                        Log.Instance.writeLog("Insertando despacho " + despacho.id_old, Log.LogType.t_debug);
                        ConectorSQLite.query(String.Format("INSERT INTO despachos ({0}) VALUES ({1})", campos, valores));
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception("Error en el método grabarDespachos. Excepcion: " + e.Message);
            }
        }

        public override void actualizarTanques()
        {
            try
            {
                // Traigo del CEM los estados de los tanques
                List<Conector.Tanque> tanques = conector.traerTanques(configEstacion.tanques.Count);

                for (int i = 0; i < tanques.Count; i++)
                {
                    int res = ConectorSQLite.query("UPDATE tanques SET volumen = '" + tanques[i].producto.Trim() + "', total = '" + (Convert.ToDouble(tanques[i].producto.Trim().Split('.')[0]) + 0.01 * Convert.ToDouble(tanques[i].producto.Trim().Split('.')[1]) + Convert.ToDouble(tanques[i].vacio.Trim().Split('.')[0]) + 0.01 * Convert.ToDouble(tanques[i].vacio.Trim().Split('.')[1])).ToString() + "' WHERE id = " + i);

                    Log.Instance.writeLog("UPDATE tanques SET volumen = '" + tanques[i].producto.Trim() + "', total = '" + (Convert.ToDouble(tanques[i].producto.Trim().Split('.')[0]) + 0.01 * Convert.ToDouble(tanques[i].producto.Trim().Split('.')[1]) + Convert.ToDouble(tanques[i].vacio.Trim().Split('.')[0]) + 0.01 * Convert.ToDouble(tanques[i].vacio.Trim().Split('.')[1])).ToString() + "' WHERE id = " + i, Log.LogType.t_debug);

                    if (res == 0)
                        ConectorSQLite.query("INSERT INTO tanques (id, volumen, total) VALUES (" +
                            "" + i + ", " +
                            "'" + tanques[i].producto.Trim() + "', " +
                            "'" + Convert.ToDouble(tanques[i].producto.Trim().Split('.')[0]) + 0.01 * Convert.ToDouble(tanques[i].producto.Trim().Split('.')[1]) + Convert.ToDouble(tanques[i].vacio.Trim().Split('.')[0]) + 0.01 * Convert.ToDouble(tanques[i].vacio.Trim().Split('.')[1]) + "')");
                }

                if (pathArchivo != "")
                {
                    string linea = "";
                    StreamWriter sw = new StreamWriter(pathArchivo);
                    for (int i = 0; i < tanques.Count; i++)
                    {
                        //linea = i.ToString() + "," + tanques[i].producto.Trim() + "," + (Convert.ToDouble(tanques[i].producto.Trim().Split('.')[0]) + 0.01 * Convert.ToDouble(tanques[i].producto.Trim().Split('.')[1]) + Convert.ToDouble(tanques[i].vacio.Trim().Split('.')[0]) + 0.01 * Convert.ToDouble(tanques[i].vacio.Trim().Split('.')[1])).ToString();
                        linea = tanques[i].producto.Trim();
                        sw.WriteLine(linea);
                    }
                    sw.Close();
                }
            }
            catch (Exception e)
            {
                throw new Exception("Error en el método traer tanques. Excepcion: " + e.Message);
            }
        }

        public override void facturaDespachos()
        {
            try
            {
                int segundos = Configuracion.leerConfiguracion().segundosFacturacion;
                if (segundos > 0)
                    ConectorSQLite.query("UPDATE despachos SET facturado = 1 WHERE fecha < datetime('now','localtime','-" + segundos + " second') AND facturado = 0");
            }
            catch (Exception e)
            {
                throw new Exception("Error en el método facturaDespachos. Excepcion: " + e.Message);
            }
        }

        public override void compruebaCierre()
        {
            try
            {
                //// Obetener configuración de la estación.
                //var configEstacion = conector.configuracionDeEstacion();

                //DataTable tabla = ConectorSQLite.dt_query("SELECT hacerCierre FROM cierreBandera");

                //if( tabla.Rows.Count > 0 )
                //{
                //    ConectorSQLite.query("DELETE FROM cierreBandera");

                //    // Cambiar por cierre de turno
                //    // CAMBIAR POR HACER CIERRE
                //    Conector.Cierre cierre = conector.traerTurnoEnCurso();

                //    string query = "INSERT INTO cierres (monto_contado, volumen_contado, monto_YPFruta, volumen_YPFruta) VALUES ({0})";
                //    string valores = String.Format(
                //        "'{0}','{1}','{2}','{3}'",
                //        cierre.totalesMediosDePago[0].monto.Trim().Substring(1), 
                //        cierre.totalesMediosDePago[0].volumen.Trim(),
                //        cierre.totalesMediosDePago[3].monto.Trim(),
                //        cierre.totalesMediosDePago[3].volumen.Trim());

                //    query = String.Format(query, valores);

                //    Log.Instance.writeLog("Insertando cierre: " + query, Log.LogType.t_debug);

                //    ConectorSQLite.query(query);

                //    // Traer ID del cierre para poder referenciar los detalles
                //    query = "SELECT max(id) FROM cierres";

                //    DataTable table = ConectorSQLite.dt_query(query);

                //    int id = Convert.ToInt32(table.Rows[0][0]);

                //    // Grabar cierresxproducto
                //    query = "INSERT INTO cierresxProducto (id, producto, monto, volumen) VALUES (" + id + ", {0})";
                //    for( int i = 0; i < cierre.totalesPorPeriodoPorNivelPorProducto[0][0].Count; i++ )
                //    {
                //        string aux = 
                //            (i+1).ToString() + "," +
                //            "'" + cierre.totalesPorPeriodoPorNivelPorProducto[0][0][i].monto.Trim() + "'," +
                //            "'" + cierre.totalesPorPeriodoPorNivelPorProducto[0][0][i].volumen.Trim() + "'";

                //        aux = String.Format(query,aux);

                //        ConectorSQLite.query(aux);
                //    }

                //    // Grabar cierresxmanguera
                //    query = "INSERT INTO cierresxManguera (id, surtidor, manguera, monto, volumen) VALUES (" + id + ", {0})";
                //    int contador = 0;
                //    for (int i = 0; i < configEstacion.surtidores.Count; i++)
                //    {
                //        for (int j = 0; j < configEstacion.surtidores[i].productoPorManguera.Count; j++)
                //        {
                //            string aux =
                //                (i + 1).ToString() + "," +
                //                (j + 1).ToString() + "," +
                //                "'" + cierre.totalesPorMangueraPorSurtidor[contador].monto.Trim() + "'," +
                //                "'" + cierre.totalesPorMangueraPorSurtidor[contador].volumen.Trim() + "'";

                //            contador++;

                //            aux = String.Format(query, aux);

                //            ConectorSQLite.query(aux);
                //        } 
                //    }
                //}
                Conector.Cierre cierre = conector.cierreDeTurno();
                Int32 i = 1;

                if (pathArchivo != "")
                {
                    string linea = "";
                    StreamWriter sw = new StreamWriter(pathArchivo);

                    i = 1;
                    foreach (Conector.Cierre.Total oTotalMedioPago in cierre.totalesMediosDePago)
                    {
                        linea = "1 Medio de pago: " + i.ToString();
                        sw.WriteLine(linea);
                        linea = "Monto: " + oTotalMedioPago.monto;
                        sw.WriteLine(linea);
                        linea = "Volumen: " + oTotalMedioPago.volumen;
                        sw.WriteLine(linea);
                        i++;
                    }

                    linea = "2 Impuesto 1: " + cierre.impuesto1;
                    sw.WriteLine(linea);
                    linea = "3 Impuesto 2: " + cierre.impuesto2;
                    sw.WriteLine(linea);

                    i = 1;
                    foreach (Conector.Cierre.TotalPorProducto oTotalProducto in cierre.totalesPorProducto)
                    {
                        linea = "4 Producto: " + i.ToString();
                        sw.WriteLine(linea);
                        linea = "Monto: " + oTotalProducto.monto;
                        sw.WriteLine(linea);
                        linea = "Volumen: " + oTotalProducto.volumen;
                        sw.WriteLine(linea);
                        linea = "Precio: " + oTotalProducto.precio;
                        sw.WriteLine(linea);
                        i++;
                    }

                    foreach (Conector.Cierre.TotalMangueraSurtidor oTotalMangueraSurtidor in cierre.totalesPorMangueraPorSurtidor)
                    {
                        linea = "5 Surtidor: " + oTotalMangueraSurtidor.idSurtidor;
                        sw.WriteLine(linea);
                        i = 1;
                        foreach (Conector.Cierre.Total oTotal in oTotalMangueraSurtidor.total)
                        {
                            linea = "Manguera: " + i.ToString();
                            sw.WriteLine(linea);
                            linea = "Monto: " + oTotal.monto;
                            sw.WriteLine(linea);
                            linea = "Volumen: " + oTotal.volumen;
                            sw.WriteLine(linea);
                            i++;
                        }
                    }
                    foreach (Conector.Cierre.TotalPorMangueraSinControl oTotalPorMangueraSinControl in cierre.totalesPorMangueraSinControl)
                    {
                        linea = "6 Surtidor: " + oTotalPorMangueraSinControl.idSurtidor;
                        sw.WriteLine(linea);
                        i = 1;
                        foreach (Conector.Cierre.Total oTotal in oTotalPorMangueraSinControl.total)
                        {
                            linea = "Manguera: " + i.ToString();
                            sw.WriteLine(linea);
                            linea = "Monto: " + oTotal.monto;
                            sw.WriteLine(linea);
                            linea = "Volumen: " + oTotal.volumen;
                            sw.WriteLine(linea);
                            i++;
                        }
                    }
                    foreach (Conector.Cierre.TotalPorMangueraPruebas oTotalPorMangueraPruebas in cierre.totalesPorMangueraPruebas)
                    {
                        linea = "7 Surtidor: " + oTotalPorMangueraPruebas.idSurtidor;
                        sw.WriteLine(linea);
                        i = 1;
                        foreach (Conector.Cierre.Total oTotal in oTotalPorMangueraPruebas.total)
                        {
                            linea = "Manguera: " + i.ToString();
                            sw.WriteLine(linea);
                            linea = "Monto: " + oTotal.monto;
                            sw.WriteLine(linea);
                            linea = "Volumen: " + oTotal.volumen;
                            sw.WriteLine(linea);
                            i++;
                        }
                    }
                    i = 1;
                    foreach (Conector.Tanque oTanque in cierre.tanques)
                    {
                        linea = "8 Tanque: " + i.ToString();
                        sw.WriteLine(linea);
                        linea = "Volumen: " + oTanque.producto;
                        sw.WriteLine(linea);
                        linea = "Agua: " + oTanque.agua;
                        sw.WriteLine(linea);
                        linea = "Vacio: " + oTanque.vacio;
                        sw.WriteLine(linea);
                        linea = "Capacidad: " + oTanque.capacidad;
                        sw.WriteLine(linea);
                        i++;
                    }
                    i = 1;
                    foreach (Conector.Producto oProducto in cierre.productos)
                    {
                        linea = "9 Producto: " + i.ToString();
                        sw.WriteLine(linea);
                        linea = "Volumen: " + oProducto.volumen;
                        sw.WriteLine(linea);
                        linea = "Agua: " + oProducto.agua;
                        sw.WriteLine(linea);
                        linea = "Vacio: " + oProducto.vacio;
                        sw.WriteLine(linea);
                        linea = "Capacidad: " + oProducto.capacidad;
                        sw.WriteLine(linea);
                        i++;
                    }
                    sw.Close();
                }

            }
            catch (Exception e)
            {
                throw new Exception("Error en el método compruebaCierre. Excepcion: " + e.Message);
            }
        }

        public override void ConsultarTurnoActual()
        {
            try
            {
                Conector.Cierre cierre = conector.traerTurnoEnCurso();
                Int32 i = 1;

                if (pathArchivo != "")
                {
                    string linea = "";
                    StreamWriter sw = new StreamWriter(pathArchivo);

                    i = 1;
                    foreach (Conector.Cierre.Total oTotalMedioPago in cierre.totalesMediosDePago)
                    {
                        linea = "1 Medio de pago: " + i.ToString();
                        sw.WriteLine(linea);
                        linea = "Monto: " + oTotalMedioPago.monto;
                        sw.WriteLine(linea);
                        linea = "Volumen: " + oTotalMedioPago.volumen;
                        sw.WriteLine(linea);
                        i++;
                    }

                    linea = "2 Impuesto 1: " + cierre.impuesto1;
                    sw.WriteLine(linea);
                    linea = "3 Impuesto 2: " + cierre.impuesto2;
                    sw.WriteLine(linea);

                    i = 1;
                    foreach (Conector.Cierre.TotalPorProducto oTotalProducto in cierre.totalesPorProducto)
                    {
                        linea = "4 Producto: " + i.ToString();
                        sw.WriteLine(linea);
                        linea = "Monto: " + oTotalProducto.monto;
                        sw.WriteLine(linea);
                        linea = "Volumen: " + oTotalProducto.volumen;
                        sw.WriteLine(linea);
                        linea = "Precio: " + oTotalProducto.precio;
                        sw.WriteLine(linea);
                        i++;
                    }

                    foreach (Conector.Cierre.TotalMangueraSurtidor oTotalMangueraSurtidor in cierre.totalesPorMangueraPorSurtidor)
                    {
                        linea = "5 Surtidor: " + oTotalMangueraSurtidor.idSurtidor;
                        sw.WriteLine(linea);
                        i = 1;
                        foreach (Conector.Cierre.Total oTotal in oTotalMangueraSurtidor.total)
                        {
                            linea = "Manguera: " + i.ToString();
                            sw.WriteLine(linea);
                            linea = "Monto: " + oTotal.monto;
                            sw.WriteLine(linea);
                            linea = "Volumen: " + oTotal.volumen;
                            sw.WriteLine(linea);
                            i++;
                        }
                    }
                    foreach (Conector.Cierre.TotalPorMangueraSinControl oTotalPorMangueraSinControl in cierre.totalesPorMangueraSinControl)
                    {
                        linea = "6 Surtidor: " + oTotalPorMangueraSinControl.idSurtidor;
                        sw.WriteLine(linea);
                        i = 1;
                        foreach (Conector.Cierre.Total oTotal in oTotalPorMangueraSinControl.total)
                        {
                            linea = "Manguera: " + i.ToString();
                            sw.WriteLine(linea);
                            linea = "Monto: " + oTotal.monto;
                            sw.WriteLine(linea);
                            linea = "Volumen: " + oTotal.volumen;
                            sw.WriteLine(linea);
                            i++;
                        }
                    }
                    foreach (Conector.Cierre.TotalPorMangueraPruebas oTotalPorMangueraPruebas in cierre.totalesPorMangueraPruebas)
                    {
                        linea = "7 Surtidor: " + oTotalPorMangueraPruebas.idSurtidor;
                        sw.WriteLine(linea);
                        i = 1;
                        foreach (Conector.Cierre.Total oTotal in oTotalPorMangueraPruebas.total)
                        {
                            linea = "Manguera: " + i.ToString();
                            sw.WriteLine(linea);
                            linea = "Monto: " + oTotal.monto;
                            sw.WriteLine(linea);
                            linea = "Volumen: " + oTotal.volumen;
                            sw.WriteLine(linea);
                            i++;
                        }
                    }
                    i = 1;
                    foreach (Conector.Tanque oTanque in cierre.tanques)
                    {
                        linea = "8 Tanque: " + i.ToString();
                        sw.WriteLine(linea);
                        linea = "Volumen: " + oTanque.producto;
                        sw.WriteLine(linea);
                        linea = "Agua: " + oTanque.agua;
                        sw.WriteLine(linea);
                        linea = "Vacio: " + oTanque.vacio;
                        sw.WriteLine(linea);
                        linea = "Capacidad: " + oTanque.capacidad;
                        sw.WriteLine(linea);
                        i++;
                    }
                    i = 1;
                    foreach (Conector.Producto oProducto in cierre.productos)
                    {
                        linea = "9 Producto: " + i.ToString();
                        sw.WriteLine(linea);
                        linea = "Volumen: " + oProducto.volumen;
                        sw.WriteLine(linea);
                        linea = "Agua: " + oProducto.agua;
                        sw.WriteLine(linea);
                        linea = "Vacio: " + oProducto.vacio;
                        sw.WriteLine(linea);
                        linea = "Capacidad: " + oProducto.capacidad;
                        sw.WriteLine(linea);
                        i++;
                    }
                    sw.Close();
                }

            }
            catch (Exception e)
            {
                throw new Exception("Error en el método ConsultarTurnoActual. Excepcion: " + e.Message);
            }
        }
    }
}
