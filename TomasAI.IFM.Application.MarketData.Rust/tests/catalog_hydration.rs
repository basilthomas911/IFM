//! Real HTTP transport -> bounded line decoder -> atomic native repository.
//! No mock decoder or second repository implementation is used in these tests.
use ifm_market_data_native::{
    catalog::{
        Definition,
        hydration::{self, Universe},
        repository::Repository,
        selection,
    },
    dns::Resolver,
    historical::History,
    runtime::Deadline,
};
use std::{
    sync::{Arc, Mutex},
    time::Duration,
};
use tokio::{
    io::{AsyncReadExt, AsyncWriteExt},
    net::TcpListener,
};
use tokio_util::sync::CancellationToken;

fn source() -> String {
    let cases: serde_json::Value =
        serde_json::from_str(include_str!("../baseline/DefinitionOracle.json")).unwrap();
    cases[0]["json"].as_str().unwrap().into()
}
async fn request(socket: &mut tokio::net::TcpStream) {
    let mut header = Vec::new();
    let mut byte = [0];
    while !header.ends_with(b"\r\n\r\n") {
        socket.read_exact(&mut byte).await.unwrap();
        header.push(byte[0]);
        assert!(header.len() < 16_384);
    }
    let length = String::from_utf8(header)
        .unwrap()
        .lines()
        .find_map(|line| {
            line.to_ascii_lowercase()
                .strip_prefix("content-length:")
                .map(|v| v.trim().parse::<usize>().unwrap())
        })
        .unwrap_or(0);
    let mut body = vec![0; length];
    socket.read_exact(&mut body).await.unwrap();
}
async fn response(socket: &mut tokio::net::TcpStream, body: &[u8], extra: usize) {
    socket
        .write_all(
            format!(
                "HTTP/1.1 200 OK\r\nContent-Length: {}\r\nConnection: close\r\n\r\n",
                body.len() + extra
            )
            .as_bytes(),
        )
        .await
        .unwrap();
    socket.write_all(body).await.unwrap();
}
#[tokio::test]
async fn provider_refresh_commits_only_after_all_lines_and_failure_or_timeout_releases_staging() {
    for mode in 0..4 {
        let json = source();
        let id = selection::format(&Definition::parse("GLBX.MDP3", &json).unwrap()).unwrap();
        let repository = Arc::new(Mutex::new(Repository::new(4, 131_072).unwrap()));
        {
            let mut repository = repository.lock().unwrap();
            let token = repository.begin().unwrap();
            repository.append(token, "GLBX.MDP3", &json).unwrap();
            repository.commit(token).unwrap();
        }
        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let history = History::new(
            &format!("http://{}/", listener.local_addr().unwrap()),
            "x".repeat(32),
            Arc::new(Resolver::new(vec!["127.0.0.1:1".parse().unwrap()]).unwrap()),
        )
        .unwrap();
        let changed = json.replace("ESZ6", "ESZ6-refreshed");
        let line = changed.clone();
        let (started, ready) = tokio::sync::oneshot::channel();
        let server = tokio::spawn(async move {
            let (mut socket, _) = listener.accept().await.unwrap();
            request(&mut socket).await;
            response(
                &mut socket,
                br#"{"start":"1990-01-01T00:00:00Z","end":"2026-12-31T00:00:00Z","schema":{"definition":{"start":"1990-01-01T00:00:00Z","end":"2026-10-25T00:00:00Z"}}}"#,
                0,
            )
            .await;
            drop(socket);
            let (mut socket, _) = listener.accept().await.unwrap();
            request(&mut socket).await;
            let body = match mode {
                1 => format!("{line}\n{{\n"),
                _ => format!("{line}\n"),
            };
            response(
                &mut socket,
                body.as_bytes(),
                if mode >= 2 { 100 } else { 0 },
            )
            .await;
            let _ = started.send(());
            if mode >= 2 {
                let mut byte = [0];
                let closed = tokio::time::timeout(Duration::from_secs(3), socket.read(&mut byte))
                    .await
                    .unwrap();
                assert!(matches!(closed, Ok(0)) || closed.is_err());
            }
        });
        let token = CancellationToken::new();
        let universes = [Universe {
            dataset: "GLBX.MDP3".into(),
            parents: vec!["ES.FUT".into()],
        }];
        let deadline = Deadline::after(Duration::from_millis(if mode == 2 { 200 } else { 3000 }));
        let work = hydration::refresh(&history, repository.clone(), &universes, &token, deadline);
        tokio::pin!(work);
        let result = if mode == 3 {
            tokio::select! {
                result = &mut work => panic!("Cancelled fixture completed early: {result:?}"),
                _ = ready => { token.cancel(); work.await }
            }
        } else {
            work.await
        };
        server.await.unwrap();
        assert_eq!(history.active_requests(), 0);
        let mut repository = repository.lock().unwrap();
        if mode == 0 {
            assert_eq!(result.unwrap(), 2);
            assert_eq!(
                repository.resolve(2, &id).unwrap().unwrap().json.as_ref(),
                changed
            );
        } else {
            assert!(result.is_err(), "mode {mode}");
            assert_eq!(repository.revision(), 1);
            assert_eq!(
                repository.resolve(1, &id).unwrap().unwrap().json.as_ref(),
                json
            );
        }
        // Successful admission after every failure proves no stuck refresh gate.
        let next = repository.begin().unwrap();
        repository.abort(next).unwrap();
    }
}
