namespace PredictIT.Tests.Infraestructura;

/// <summary>
/// Las pruebas que mueven el estado del usuario <c>solicitante</c> no corren en
/// paralelo entre sí.
///
/// Son cinco. Dos hacen lo contrario entre sí: una cuenta intentos fallidos
/// hasta que el usuario queda bloqueado, y la otra lo desbloquea para partir de
/// cero. Las otras tres **entran** como <c>solicitante</c> con la contraseña
/// correcta para pedir un token, y un inicio de sesión correcto pone el contador
/// de intentos en cero: cruzada con las de bloqueo, desbloquea la cuenta entre
/// los cinco intentos fallidos y la verificación.
///
/// xUnit corre las clases en paralelo, así que cruzadas fallan por turnos sin
/// que haya nada roto en el sistema. Con la colección compartida corren una
/// después de la otra.
///
/// Las tres de integración se sumaron después de que
/// <c>La_cuenta_bloqueada_se_distingue_de_las_credenciales_invalidas</c> fallara
/// una vez en una corrida completa y pasara aislada, que es la firma exacta de
/// este problema. Cualquier clase nueva que inicie sesión como
/// <c>solicitante</c> tiene que entrar acá también.
///
/// No se usa un usuario de prueba dedicado a propósito: el que importa es el
/// del seed, porque es el que va a usar quien evalúe.
/// </summary>
[CollectionDefinition(Nombre)]
public class UsuarioSolicitante
{
    public const string Nombre = "usuario solicitante";
}
